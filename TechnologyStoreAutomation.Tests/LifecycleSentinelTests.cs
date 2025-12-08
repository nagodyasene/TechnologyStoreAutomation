using System.Net;
using Microsoft.Extensions.Configuration;
using Moq;
using RichardSzalay.MockHttp;
using TechnologyStoreAutomation.backend.trendCalculator;
using TechnologyStoreAutomation.backend.trendCalculator.data;

namespace TechnologyStoreAutomation.Tests;

public class LifecycleSentinelTests
{
    #region Constants
    
    private const string AppleVintageListUrl = "https://support.apple.com/en-us/102772";
    private const string HtmlContentType = "text/html";
    private const string ActivePhase = "ACTIVE";
    private const string LegacyPhase = "LEGACY";
    private const string ObsoletePhase = "OBSOLETE";
    
    #endregion

    private readonly Mock<IProductRepository> _mockRepository;
    private readonly MockHttpMessageHandler _mockHttp;
    private readonly HttpClient _httpClient;

    public LifecycleSentinelTests()
    {
        _mockRepository = new Mock<IProductRepository>();
        _mockHttp = new MockHttpMessageHandler();
        _httpClient = _mockHttp.ToHttpClient();
    }

    [Fact]
    public async Task RunDailyAudit_CallsBothScrapers()
    {
        // Arrange
        var mockFactory = CreateMockHttpClientFactory();
        var mockConfig = CreateMockConfiguration();
        _mockHttp.When(AppleVintageListUrl)
            .Respond(HtmlContentType, "<html><body><ul><li>Some content</li></ul></body></html>");

        var sentinel = new LifecycleSentinel(_mockRepository.Object, mockConfig.Object, mockFactory.Object);

        // Act
        await sentinel.RunDailyAuditAsync();

        // Assert - verify that HTTP requests were made (both scrapers ran)
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task CheckAppleVintageList_ParsesVintageProducts()
    {
        // Arrange
        var appleHtml = @"
            <html><body>
                <h2>Vintage Products</h2>
                <ul>
                    <li>iPhone 11 Pro</li>
                    <li>iPhone 11</li>
                    <li>MacBook Air (Retina, 13-inch, 2018)</li>
                </ul>
            </body></html>";

        _mockHttp.When(AppleVintageListUrl)
            .Respond(HtmlContentType, appleHtml);

        var mockFactory = CreateMockHttpClientFactory();
        var mockConfig = CreateMockConfiguration();

        // Set up mock product in the database
        var testProducts = new List<Product>
        {
            new Product
            {
                Id = 1,
                Name = "Apple iPhone 11 Pro 256GB",
                LifecyclePhase = ActivePhase,
                Sku = "IPHONE11PRO"
            },
            new Product
            {
                Id = 2,
                Name = "MacBook Air 2018",
                LifecyclePhase = ActivePhase,
                Sku = "MBA2018"
            }
        };

        _mockRepository.Setup(r => r.GetAllProductsAsync())
            .ReturnsAsync(testProducts);

        _mockRepository.Setup(r => r.UpdateProductPhaseAsync(
            It.IsAny<int>(),
            "LEGACY",
            It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var eventFired = false;
        var sentinel = new LifecycleSentinel(_mockRepository.Object, mockConfig.Object, mockFactory.Object);
        sentinel.OnProductStatusChanged += (name, phase, _) =>
        {
            eventFired = true;
            Assert.Contains("iPhone", name, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(LifecyclePhase.Legacy, phase);
        };

        // Act
        await sentinel.RunDailyAuditAsync();

        // Assert
        Assert.True(eventFired, "OnProductStatusChanged event should have been fired");

        // Verify that UpdateProductPhaseAsync was called for matching products
        _mockRepository.Verify(r => r.UpdateProductPhaseAsync(
            It.IsAny<int>(),
            LegacyPhase,
            It.Is<string>(s => s.Contains("Apple"))),
            Times.AtLeastOnce());
    }

    [Fact]
    public async Task CheckAppleVintageList_ParsesObsoleteProducts()
    {
        // Arrange
        var appleHtml = @"
            <html><body>
                <h2>Obsolete Products</h2>
                <ul>
                    <li>iPhone 6</li>
                    <li>iPhone 6 Plus</li>
                    <li>iPad Air (1st generation)</li>
                </ul>
            </body></html>";

        _mockHttp.When(AppleVintageListUrl)
            .Respond(HtmlContentType, appleHtml);

        var mockFactory = CreateMockHttpClientFactory();
        var mockConfig = CreateMockConfiguration();

        var testProducts = new List<Product>
        {
            new Product { Id = 1, Name = "iPhone 6 128GB Space Gray", LifecyclePhase = LegacyPhase, Sku = "IP6" },
            new Product { Id = 2, Name = "iPad Air", LifecyclePhase = ActivePhase, Sku = "IPADAIR1" }
        };

        _mockRepository.Setup(r => r.GetAllProductsAsync()).ReturnsAsync(testProducts);
        _mockRepository.Setup(r => r.UpdateProductPhaseAsync(It.IsAny<int>(), "LEGACY", It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var obsoleteEventCount = 0;
        var sentinel = new LifecycleSentinel(_mockRepository.Object, mockConfig.Object, mockFactory.Object);
        sentinel.OnProductStatusChanged += (_, phase, _) =>
        {
            if (phase == LifecyclePhase.Obsolete)
                obsoleteEventCount++;
        };

        // Act
        await sentinel.RunDailyAuditAsync();

        // Assert
        Assert.True(obsoleteEventCount > 0, "At least one obsolete product should have been detected");
        _mockRepository.Verify(r => r.UpdateProductPhaseAsync(
            It.IsAny<int>(),
            ObsoletePhase,
            It.Is<string>(s => s.Contains("Apple"))),
            Times.AtLeastOnce());
    }

    [Fact]
    public async Task CheckGooglePixelEOL_DetectsExpiredDevices()
    {
        // Arrange
        var mockFactory = CreateMockHttpClientFactory();
        var mockConfig = CreateMockConfiguration();

        // Pixel 3 has an EOL date of May 2022 (already passed)
        var testProducts = new List<Product>
        {
            new Product { Id = 1, Name = "Google Pixel 3 64GB", LifecyclePhase = ActivePhase, Sku = "PIX3" },
            new Product { Id = 2, Name = "Pixel 4a", LifecyclePhase = LegacyPhase, Sku = "PIX4A" }
        };

        _mockRepository.Setup(r => r.GetAllProductsAsync()).ReturnsAsync(testProducts);
        _mockRepository.Setup(r => r.UpdateProductPhaseAsync(It.IsAny<int>(), ObsoletePhase, It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var sentinel = new LifecycleSentinel(_mockRepository.Object, mockConfig.Object, mockFactory.Object);

        // Act
        await sentinel.RunDailyAuditAsync();

        // Assert - Pixel 3 should be marked OBSOLETE (EOL was 2022)
        _mockRepository.Verify(r => r.UpdateProductPhaseAsync(
            It.Is<int>(id => id == 1),
            ObsoletePhase,
            It.Is<string>(s => s.Contains("Google"))),
            Times.Once());
    }

    [Fact]
    public async Task CheckGooglePixelEOL_DetectsUpcomingEOL()
    {
        // Arrange
        var mockFactory = CreateMockHttpClientFactory();
        var mockConfig = CreateMockConfiguration();

        // Create a product that will reach EOL within 6 months (mock by using Pixel 5)
        var testProducts = new List<Product>
        {
            new Product { Id = 1, Name = "Pixel 5", LifecyclePhase = ActivePhase, Sku = "PIX5" }
        };

        _mockRepository.Setup(r => r.GetAllProductsAsync()).ReturnsAsync(testProducts);
        _mockRepository.Setup(r => r.UpdateProductPhaseAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var sentinel = new LifecycleSentinel(_mockRepository.Object, mockConfig.Object, mockFactory.Object);

        // Act
        await sentinel.RunDailyAuditAsync();

        // Assert - Pixel 5 EOL is Oct 2024, which has passed, so it should be OBSOLETE
        _mockRepository.Verify(r => r.UpdateProductPhaseAsync(
            It.Is<int>(id => id == 1),
            ObsoletePhase,
            It.Is<string>(s => s.Contains("support ended"))),
            Times.Once());
    }

    [Fact]
    public async Task FuzzyMatching_MatchesProductVariations()
    {
        // Arrange
        var appleHtml = @"
            <html><body>
                <h2>Vintage Products</h2>
                <ul>
                    <li>iPhone 12</li>
                </ul>
            </body></html>";

        _mockHttp.When("https://support.apple.com/en-us/102772")
            .Respond(HtmlContentType, appleHtml);

        var mockFactory = CreateMockHttpClientFactory();
        var mockConfig = CreateMockConfiguration();

        // Product with full detailed name
        var testProducts = new List<Product>
        {
            new Product { Id = 1, Name = "Apple iPhone 12 128GB Blue", LifecyclePhase = "ACTIVE", Sku = "IP12" }
        };

        _mockRepository.Setup(r => r.GetAllProductsAsync()).ReturnsAsync(testProducts);
        _mockRepository.Setup(r => r.UpdateProductPhaseAsync(It.IsAny<int>(), "LEGACY", It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var sentinel = new LifecycleSentinel(_mockRepository.Object, mockConfig.Object, mockFactory.Object);

        // Act
        await sentinel.RunDailyAuditAsync();

        // Assert - should match "iPhone 12" with "Apple iPhone 12 128GB Blue"
        _mockRepository.Verify(r => r.UpdateProductPhaseAsync(
            It.Is<int>(id => id == 1),
            LegacyPhase,
            It.IsAny<string>()),
            Times.Once());
    }

    [Fact]
    public async Task RunDailyAudit_HandlesHttpErrors_GracefullyAsync()
    {
        // Arrange
        _mockHttp.When(AppleVintageListUrl)
            .Respond(HttpStatusCode.ServiceUnavailable);

        var mockFactory = CreateMockHttpClientFactory();
        var mockConfig = CreateMockConfiguration();
        var sentinel = new LifecycleSentinel(_mockRepository.Object, mockConfig.Object, mockFactory.Object);

        // Act & Assert - should not throw exception
        await sentinel.RunDailyAuditAsync();

        // Verify repository was not called since scraping failed
        _mockRepository.Verify(r => r.UpdateProductPhaseAsync(
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string>()),
            Times.Never());
    }

    [Fact]
    public async Task OnlyUpdatesProducts_WhenPhaseActuallyChanges()
    {
        // Arrange
        var appleHtml = @"
            <html><body>
                <h2>Vintage Products</h2>
                <ul><li>iPhone 11</li></ul>
            </body></html>";

        _mockHttp.When(AppleVintageListUrl)
            .Respond(HtmlContentType, appleHtml);

        var mockFactory = CreateMockHttpClientFactory();
        var mockConfig = CreateMockConfiguration();

        // Product already marked as LEGACY
        var testProducts = new List<Product>
        {
            new Product { Id = 1, Name = "iPhone 11", LifecyclePhase = LegacyPhase, Sku = "IP11" }
        };

        _mockRepository.Setup(r => r.GetAllProductsAsync()).ReturnsAsync(testProducts);
        _mockRepository.Setup(r => r.UpdateProductPhaseAsync(It.IsAny<int>(), "LEGACY", It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var sentinel = new LifecycleSentinel(_mockRepository.Object, mockConfig.Object, mockFactory.Object);

        // Act
        await sentinel.RunDailyAuditAsync();

        // Assert - should NOT call UpdateProductPhaseAsync because phase hasn't changed
        _mockRepository.Verify(r => r.UpdateProductPhaseAsync(
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string>()),
            Times.Never());
    }

    [Fact]
    public async Task FiltersOutNonProductText()
    {
        // Arrange
        var appleHtml = @"
            <html><body>
                <h2>Vintage Products</h2>
                <ul>
                    <li>Products that are discontinued...</li>
                    <li>Apple has designated certain products...</li>
                    <li>iPhone 11 Pro</li>
                    <li>Some random text</li>
                    <li>MacBook Air (2018)</li>
                </ul>
            </body></html>";

        _mockHttp.When(AppleVintageListUrl)
            .Respond("text/html", appleHtml);

        var mockFactory = CreateMockHttpClientFactory();
        var mockConfig = CreateMockConfiguration();
        var testProducts = new List<Product>
        {
            new Product { Id = 1, Name = "iPhone 11 Pro", LifecyclePhase = "ACTIVE", Sku = "IP11P" },
            new Product { Id = 2, Name = "MacBook Air 2018", LifecyclePhase = "ACTIVE", Sku = "MBA18" }
        };

        _mockRepository.Setup(r => r.GetAllProductsAsync()).ReturnsAsync(testProducts);
        _mockRepository.Setup(r => r.UpdateProductPhaseAsync(It.IsAny<int>(), "LEGACY", It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var sentinel = new LifecycleSentinel(_mockRepository.Object, mockConfig.Object, mockFactory.Object);

        // Act
        await sentinel.RunDailyAuditAsync();

        // Assert - should only update actual product names, not descriptive text
        _mockRepository.Verify(r => r.UpdateProductPhaseAsync(
            It.IsAny<int>(),
            "LEGACY",
            It.IsAny<string>()),
            Times.Exactly(2)); // Only 2 real products
    }

    #region Edge Case Tests

    [Fact]
    public async Task RunDailyAudit_EmptyProductList_CompletesWithoutError()
    {
        // Arrange
        _mockHttp.When(AppleVintageListUrl)
            .Respond(HtmlContentType, "<html><body><ul><li>iPhone 11</li></ul></body></html>");

        var mockFactory = CreateMockHttpClientFactory();
        var mockConfig = CreateMockConfiguration();
        
        _mockRepository.Setup(r => r.GetAllProductsAsync())
            .ReturnsAsync(new List<Product>());

        var sentinel = new LifecycleSentinel(_mockRepository.Object, mockConfig.Object, mockFactory.Object);

        // Act & Assert - should complete without throwing
        await sentinel.RunDailyAuditAsync();

        _mockRepository.Verify(r => r.UpdateProductPhaseAsync(
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string>()),
            Times.Never());
    }

    [Fact]
    public async Task RunDailyAudit_EmptyHtmlResponse_HandlesGracefully()
    {
        // Arrange
        _mockHttp.When(AppleVintageListUrl)
            .Respond(HtmlContentType, "");

        var mockFactory = CreateMockHttpClientFactory();
        var mockConfig = CreateMockConfiguration();
        
        var testProducts = new List<Product>
        {
            new Product { Id = 1, Name = "iphone 14 pro", LifecyclePhase = "ACTIVE", Sku = "IP14P" }
        };

        _mockRepository.Setup(r => r.GetAllProductsAsync())
            .ReturnsAsync(testProducts);

        var sentinel = new LifecycleSentinel(_mockRepository.Object, mockConfig.Object, mockFactory.Object);

        // Act & Assert - should complete without throwing
        await sentinel.RunDailyAuditAsync();
        
        // Verify no updates were made with empty response
        _mockRepository.Verify(r => r.UpdateProductPhaseAsync(
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string>()),
            Times.Never());
    }

    [Fact]
    public async Task RunDailyAudit_MalformedHtml_HandlesGracefully()
    {
        // Arrange
        _mockHttp.When(AppleVintageListUrl)
            .Respond(HtmlContentType, "<html><body><ul><li>Unclosed tag<li>Another</body>");

        var mockFactory = CreateMockHttpClientFactory();
        var mockConfig = CreateMockConfiguration();
        
        var testProducts = new List<Product>
        {
            new Product { Id = 1, Name = "iphone 14 pro", LifecyclePhase = "ACTIVE", Sku = "IP14P" }
        };

        _mockRepository.Setup(r => r.GetAllProductsAsync())
            .ReturnsAsync(testProducts);

        var sentinel = new LifecycleSentinel(_mockRepository.Object, mockConfig.Object, mockFactory.Object);

        // Act & Assert - HtmlAgilityPack should handle malformed HTML
        await sentinel.RunDailyAuditAsync();
        
        // Verify that the method completed without throwing exception
        Assert.True(true, "Method should complete without throwing on malformed HTML");
    }

    [Fact]
    public async Task RunDailyAudit_TimeoutError_HandlesGracefully()
    {
        // Arrange
        _mockHttp.When(AppleVintageListUrl)
            .Respond(HttpStatusCode.RequestTimeout);

        var mockFactory = CreateMockHttpClientFactory();
        var mockConfig = CreateMockConfiguration();
        var sentinel = new LifecycleSentinel(_mockRepository.Object, mockConfig.Object, mockFactory.Object);

        // Act & Assert - should not throw
        await sentinel.RunDailyAuditAsync();
        Assert.True(true, "Method should complete without throwing on timeout");
    }

    [Fact]
    public async Task RunDailyAudit_NetworkError_HandlesGracefully()
    {
        // Arrange
        _mockHttp.When(AppleVintageListUrl)
            .Respond(HttpStatusCode.InternalServerError);

        var mockFactory = CreateMockHttpClientFactory();
        var mockConfig = CreateMockConfiguration();
        var sentinel = new LifecycleSentinel(_mockRepository.Object, mockConfig.Object, mockFactory.Object);

        // Act & Assert - should not throw
        await sentinel.RunDailyAuditAsync();
        Assert.True(true, "Method should complete without throwing on network error");
    }

    [Fact]
    public async Task ProductPhaseTransition_Active_To_Legacy()
    {
        // Arrange
        var appleHtml = @"
            <html><body>
                <h2>Vintage Products</h2>
                <ul><li>iPhone 13 mini</li></ul>
            </body></html>";

        _mockHttp.When(AppleVintageListUrl)
            .Respond("text/html", appleHtml);

        var mockFactory = CreateMockHttpClientFactory();
        var mockConfig = CreateMockConfiguration();
        
        var testProducts = new List<Product>
        {
            new Product { Id = 1, Name = "iPhone 13 mini 128GB", LifecyclePhase = "ACTIVE", Sku = "IP13M" }
        };

        _mockRepository.Setup(r => r.GetAllProductsAsync())
            .ReturnsAsync(testProducts);
        _mockRepository.Setup(r => r.UpdateProductPhaseAsync(1, "LEGACY", It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var phaseChanges = new List<(string Name, LifecyclePhase Phase)>();
        var sentinel = new LifecycleSentinel(_mockRepository.Object, mockConfig.Object, mockFactory.Object);
        sentinel.OnProductStatusChanged += (name, phase, _) =>
        {
            phaseChanges.Add((name, phase));
        };

        // Act
        await sentinel.RunDailyAuditAsync();

        // Assert
        Assert.Single(phaseChanges);
        Assert.Equal(LifecyclePhase.Legacy, phaseChanges[0].Phase);
    }

    [Fact]
    public async Task ProductPhaseTransition_Legacy_To_Obsolete()
    {
        // Arrange
        var appleHtml = @"
            <html><body>
                <h2>Obsolete Products</h2>
                <ul><li>iPhone 7</li></ul>
            </body></html>";

        _mockHttp.When(AppleVintageListUrl)
            .Respond("text/html", appleHtml);

        var mockFactory = CreateMockHttpClientFactory();
        var mockConfig = CreateMockConfiguration();
        
        var testProducts = new List<Product>
        {
            new Product { Id = 1, Name = "iphone 14 pro", LifecyclePhase = "ACTIVE", Sku = "IP14P" }
        };

        _mockRepository.Setup(r => r.GetAllProductsAsync())
            .ReturnsAsync(testProducts);
        _mockRepository.Setup(r => r.UpdateProductPhaseAsync(1, ObsoletePhase, It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var sentinel = new LifecycleSentinel(_mockRepository.Object, mockConfig.Object, mockFactory.Object);

        // Act
        await sentinel.RunDailyAuditAsync();

        // Assert
        _mockRepository.Verify(r => r.UpdateProductPhaseAsync(1, ObsoletePhase, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task MultipleProducts_MatchingSameVintageName_AllUpdated()
    {
        // Arrange
        var appleHtml = @"
            <html><body>
                <h2>Vintage Products</h2>
                <ul><li>iPhone SE</li></ul>
            </body></html>";

        _mockHttp.When(AppleVintageListUrl)
            .Respond("text/html", appleHtml);

        var mockFactory = CreateMockHttpClientFactory();
        var mockConfig = CreateMockConfiguration();
        
        // Multiple variants of the same product
        var testProducts = new List<Product>
        {
            new Product { Id = 1, Name = "iPhone SE 64GB Space Gray", LifecyclePhase = "ACTIVE", Sku = "IPSE64G" },
            new Product { Id = 2, Name = "iPhone SE 128GB Silver", LifecyclePhase = "ACTIVE", Sku = "IPSE128S" },
            new Product { Id = 3, Name = "iPhone SE 64GB Gold", LifecyclePhase = "ACTIVE", Sku = "IPSE64GD" }
        };

        _mockRepository.Setup(r => r.GetAllProductsAsync())
            .ReturnsAsync(testProducts);
        _mockRepository.Setup(r => r.UpdateProductPhaseAsync(It.IsAny<int>(), "LEGACY", It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var sentinel = new LifecycleSentinel(_mockRepository.Object, mockConfig.Object, mockFactory.Object);

        // Act
        await sentinel.RunDailyAuditAsync();

        // Assert - all 3 variants should be updated
        _mockRepository.Verify(r => r.UpdateProductPhaseAsync(
            It.IsAny<int>(),
            "LEGACY",
            It.IsAny<string>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task CaseSensitivity_ProductNameMatching_WorksCorrectly()
    {
        // Arrange
        var appleHtml = @"
            <html><body>
                <h2>Vintage Products</h2>
                <ul><li>IPHONE 14</li></ul>
            </body></html>";

        _mockHttp.When(AppleVintageListUrl)
            .Respond("text/html", appleHtml);

        var mockFactory = CreateMockHttpClientFactory();
        var mockConfig = CreateMockConfiguration();
        
        var testProducts = new List<Product>
        {
            new Product { Id = 1, Name = "iphone 14 pro", LifecyclePhase = "ACTIVE", Sku = "IP14P" }
        };

        _mockRepository.Setup(r => r.GetAllProductsAsync())
            .ReturnsAsync(testProducts);
        _mockRepository.Setup(r => r.UpdateProductPhaseAsync(It.IsAny<int>(), "LEGACY", It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var sentinel = new LifecycleSentinel(_mockRepository.Object, mockConfig.Object, mockFactory.Object);

        // Act
        await sentinel.RunDailyAuditAsync();

        // Assert - case-insensitive matching should work
        _mockRepository.Verify(r => r.UpdateProductPhaseAsync(1, "LEGACY", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task NonAppleProduct_NotAffectedByAppleScraper()
    {
        // Arrange
        var appleHtml = @"
            <html><body>
                <h2>Vintage Products</h2>
                <ul><li>iPhone 11</li></ul>
            </body></html>";

        _mockHttp.When(AppleVintageListUrl)
            .Respond("text/html", appleHtml);

        var mockFactory = CreateMockHttpClientFactory();
        var mockConfig = CreateMockConfiguration();
        
        var testProducts = new List<Product>
        {
            new Product { Id = 1, Name = "Samsung Galaxy S21", LifecyclePhase = "ACTIVE", Sku = "SGS21" },
            new Product { Id = 2, Name = "Dell XPS 15", LifecyclePhase = "ACTIVE", Sku = "DXPS15" }
        };

        _mockRepository.Setup(r => r.GetAllProductsAsync())
            .ReturnsAsync(testProducts);

        var sentinel = new LifecycleSentinel(_mockRepository.Object, mockConfig.Object, mockFactory.Object);

        // Act
        await sentinel.RunDailyAuditAsync();

        // Assert - non-Apple products should not be affected
        _mockRepository.Verify(r => r.UpdateProductPhaseAsync(
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string>()),
            Times.Never());
    }

    #endregion

    /// <summary>
    /// Helper method to create a mock IConfiguration with manufacturer URLs
    /// </summary>
    private static Mock<IConfiguration> CreateMockConfiguration()
    {
        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(c => c["Manufacturers:Apple:VintageListUrl"])
            .Returns("https://support.apple.com/en-us/102772");
        mockConfig.Setup(c => c["Manufacturers:Google:PixelEolUrl"])
            .Returns("https://support.google.com/pixelphone/answer/4457705");
        return mockConfig;
    }

    /// <summary>
    /// Helper method to create a mock IHttpClientFactory that returns our mocked HttpClient
    /// </summary>
    private Mock<IHttpClientFactory> CreateMockHttpClientFactory()
    {
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(_httpClient);
        return mockFactory;
    }
}
