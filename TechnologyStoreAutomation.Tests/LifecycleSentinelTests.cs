using System.Net;
using Moq;
using RichardSzalay.MockHttp;
using TechnologyStoreAutomation.backend.trendCalculator;
using TechnologyStoreAutomation.backend.trendCalculator.data;

namespace TechnologyStoreAutomation.Tests;

public class LifecycleSentinelTests
{
    private Mock<IProductRepository> _mockRepository;
    private MockHttpMessageHandler _mockHttp;
    private HttpClient _httpClient;

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
        _mockHttp.When("https://support.apple.com/en-us/102772")
            .Respond("text/html", "<html><body><ul><li>Some content</li></ul></body></html>");

        var sentinel = new LifecycleSentinel(_mockRepository.Object, mockFactory.Object);

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

        _mockHttp.When("https://support.apple.com/en-us/102772")
            .Respond("text/html", appleHtml);

        var mockFactory = CreateMockHttpClientFactory();

        // Setup mock product in database
        var testProducts = new List<Product>
        {
            new Product
            {
                Id = 1,
                Name = "Apple iPhone 11 Pro 256GB",
                LifecyclePhase = "ACTIVE",
                Sku = "IPHONE11PRO"
            },
            new Product
            {
                Id = 2,
                Name = "MacBook Air 2018",
                LifecyclePhase = "ACTIVE",
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
        var sentinel = new LifecycleSentinel(_mockRepository.Object, mockFactory.Object);
        sentinel.OnProductStatusChanged += (name, phase, reason) =>
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
            "LEGACY",
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

        _mockHttp.When("https://support.apple.com/en-us/102772")
            .Respond("text/html", appleHtml);

        var mockFactory = CreateMockHttpClientFactory();

        var testProducts = new List<Product>
        {
            new Product { Id = 1, Name = "iPhone 6 128GB Space Gray", LifecyclePhase = "LEGACY", Sku = "IP6" },
            new Product { Id = 2, Name = "iPad Air", LifecyclePhase = "ACTIVE", Sku = "IPADAIR1" }
        };

        _mockRepository.Setup(r => r.GetAllProductsAsync()).ReturnsAsync(testProducts);
        _mockRepository.Setup(r => r.UpdateProductPhaseAsync(It.IsAny<int>(), "OBSOLETE", It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var obsoleteEventCount = 0;
        var sentinel = new LifecycleSentinel(_mockRepository.Object, mockFactory.Object);
        sentinel.OnProductStatusChanged += (name, phase, reason) =>
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
            "OBSOLETE",
            It.Is<string>(s => s.Contains("Apple"))),
            Times.AtLeastOnce());
    }

    [Fact]
    public async Task CheckGooglePixelEOL_DetectsExpiredDevices()
    {
        // Arrange
        var mockFactory = CreateMockHttpClientFactory();

        // Pixel 3 has EOL date of May 2022 (already passed)
        var testProducts = new List<Product>
        {
            new Product { Id = 1, Name = "Google Pixel 3 64GB", LifecyclePhase = "ACTIVE", Sku = "PIX3" },
            new Product { Id = 2, Name = "Pixel 4a", LifecyclePhase = "LEGACY", Sku = "PIX4A" }
        };

        _mockRepository.Setup(r => r.GetAllProductsAsync()).ReturnsAsync(testProducts);
        _mockRepository.Setup(r => r.UpdateProductPhaseAsync(It.IsAny<int>(), "OBSOLETE", It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var sentinel = new LifecycleSentinel(_mockRepository.Object, mockFactory.Object);

        // Act
        await sentinel.RunDailyAuditAsync();

        // Assert - Pixel 3 should be marked OBSOLETE (EOL was 2022)
        _mockRepository.Verify(r => r.UpdateProductPhaseAsync(
            It.Is<int>(id => id == 1),
            "OBSOLETE",
            It.Is<string>(s => s.Contains("Google"))),
            Times.Once());
    }

    [Fact]
    public async Task CheckGooglePixelEOL_DetectsUpcomingEOL()
    {
        // Arrange
        var mockFactory = CreateMockHttpClientFactory();

        // Create a product that will reach EOL within 6 months (mock by using Pixel 5)
        var testProducts = new List<Product>
        {
            new Product { Id = 1, Name = "Pixel 5", LifecyclePhase = "ACTIVE", Sku = "PIX5" }
        };

        _mockRepository.Setup(r => r.GetAllProductsAsync()).ReturnsAsync(testProducts);
        _mockRepository.Setup(r => r.UpdateProductPhaseAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var sentinel = new LifecycleSentinel(_mockRepository.Object, mockFactory.Object);

        // Act
        await sentinel.RunDailyAuditAsync();

        // Assert - Pixel 5 EOL is Oct 2024, which has passed, so it should be OBSOLETE
        _mockRepository.Verify(r => r.UpdateProductPhaseAsync(
            It.Is<int>(id => id == 1),
            "OBSOLETE",
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
            .Respond("text/html", appleHtml);

        var mockFactory = CreateMockHttpClientFactory();

        // Product with full detailed name
        var testProducts = new List<Product>
        {
            new Product { Id = 1, Name = "Apple iPhone 12 128GB Blue", LifecyclePhase = "ACTIVE", Sku = "IP12" }
        };

        _mockRepository.Setup(r => r.GetAllProductsAsync()).ReturnsAsync(testProducts);
        _mockRepository.Setup(r => r.UpdateProductPhaseAsync(It.IsAny<int>(), "LEGACY", It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var sentinel = new LifecycleSentinel(_mockRepository.Object, mockFactory.Object);

        // Act
        await sentinel.RunDailyAuditAsync();

        // Assert - should match "iPhone 12" with "Apple iPhone 12 128GB Blue"
        _mockRepository.Verify(r => r.UpdateProductPhaseAsync(
            It.Is<int>(id => id == 1),
            "LEGACY",
            It.IsAny<string>()),
            Times.Once());
    }

    [Fact]
    public async Task RunDailyAudit_HandlesHttpErrors_GracefullyAsync()
    {
        // Arrange
        _mockHttp.When("https://support.apple.com/en-us/102772")
            .Respond(HttpStatusCode.ServiceUnavailable);

        var mockFactory = CreateMockHttpClientFactory();
        var sentinel = new LifecycleSentinel(_mockRepository.Object, mockFactory.Object);

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

        _mockHttp.When("https://support.apple.com/en-us/102772")
            .Respond("text/html", appleHtml);

        var mockFactory = CreateMockHttpClientFactory();

        // Product already marked as LEGACY
        var testProducts = new List<Product>
        {
            new Product { Id = 1, Name = "iPhone 11", LifecyclePhase = "LEGACY", Sku = "IP11" }
        };

        _mockRepository.Setup(r => r.GetAllProductsAsync()).ReturnsAsync(testProducts);
        _mockRepository.Setup(r => r.UpdateProductPhaseAsync(It.IsAny<int>(), "LEGACY", It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var sentinel = new LifecycleSentinel(_mockRepository.Object, mockFactory.Object);

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

        _mockHttp.When("https://support.apple.com/en-us/102772")
            .Respond("text/html", appleHtml);

        var mockFactory = CreateMockHttpClientFactory();
        var testProducts = new List<Product>
        {
            new Product { Id = 1, Name = "iPhone 11 Pro", LifecyclePhase = "ACTIVE", Sku = "IP11P" },
            new Product { Id = 2, Name = "MacBook Air 2018", LifecyclePhase = "ACTIVE", Sku = "MBA18" }
        };

        _mockRepository.Setup(r => r.GetAllProductsAsync()).ReturnsAsync(testProducts);
        _mockRepository.Setup(r => r.UpdateProductPhaseAsync(It.IsAny<int>(), "LEGACY", It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var sentinel = new LifecycleSentinel(_mockRepository.Object, mockFactory.Object);

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
        _mockHttp.When("https://support.apple.com/en-us/102772")
            .Respond("text/html", "<html><body><ul><li>iPhone 11</li></ul></body></html>");

        var mockFactory = CreateMockHttpClientFactory();
        
        _mockRepository.Setup(r => r.GetAllProductsAsync())
            .ReturnsAsync(new List<Product>());

        var sentinel = new LifecycleSentinel(_mockRepository.Object, mockFactory.Object);

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
        _mockHttp.When("https://support.apple.com/en-us/102772")
            .Respond("text/html", "");

        var mockFactory = CreateMockHttpClientFactory();
        
        var testProducts = new List<Product>
        {
            new Product { Id = 1, Name = "iPhone 11", LifecyclePhase = "ACTIVE", Sku = "IP11" }
        };

        _mockRepository.Setup(r => r.GetAllProductsAsync())
            .ReturnsAsync(testProducts);

        var sentinel = new LifecycleSentinel(_mockRepository.Object, mockFactory.Object);

        // Act & Assert - should complete without throwing
        await sentinel.RunDailyAuditAsync();
    }

    [Fact]
    public async Task RunDailyAudit_MalformedHtml_HandlesGracefully()
    {
        // Arrange
        _mockHttp.When("https://support.apple.com/en-us/102772")
            .Respond("text/html", "<html><body><ul><li>Unclosed tag<li>Another</body>");

        var mockFactory = CreateMockHttpClientFactory();
        
        var testProducts = new List<Product>
        {
            new Product { Id = 1, Name = "iPhone 11", LifecyclePhase = "ACTIVE", Sku = "IP11" }
        };

        _mockRepository.Setup(r => r.GetAllProductsAsync())
            .ReturnsAsync(testProducts);

        var sentinel = new LifecycleSentinel(_mockRepository.Object, mockFactory.Object);

        // Act & Assert - HtmlAgilityPack should handle malformed HTML
        await sentinel.RunDailyAuditAsync();
    }

    [Fact]
    public async Task RunDailyAudit_TimeoutError_HandlesGracefully()
    {
        // Arrange
        _mockHttp.When("https://support.apple.com/en-us/102772")
            .Respond(HttpStatusCode.RequestTimeout);

        var mockFactory = CreateMockHttpClientFactory();
        var sentinel = new LifecycleSentinel(_mockRepository.Object, mockFactory.Object);

        // Act & Assert - should not throw
        await sentinel.RunDailyAuditAsync();
    }

    [Fact]
    public async Task RunDailyAudit_NetworkError_HandlesGracefully()
    {
        // Arrange
        _mockHttp.When("https://support.apple.com/en-us/102772")
            .Respond(HttpStatusCode.InternalServerError);

        var mockFactory = CreateMockHttpClientFactory();
        var sentinel = new LifecycleSentinel(_mockRepository.Object, mockFactory.Object);

        // Act & Assert - should not throw
        await sentinel.RunDailyAuditAsync();
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

        _mockHttp.When("https://support.apple.com/en-us/102772")
            .Respond("text/html", appleHtml);

        var mockFactory = CreateMockHttpClientFactory();
        
        var testProducts = new List<Product>
        {
            new Product { Id = 1, Name = "iPhone 13 mini 128GB", LifecyclePhase = "ACTIVE", Sku = "IP13M" }
        };

        _mockRepository.Setup(r => r.GetAllProductsAsync())
            .ReturnsAsync(testProducts);
        _mockRepository.Setup(r => r.UpdateProductPhaseAsync(1, "LEGACY", It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var phaseChanges = new List<(string Name, LifecyclePhase Phase)>();
        var sentinel = new LifecycleSentinel(_mockRepository.Object, mockFactory.Object);
        sentinel.OnProductStatusChanged += (name, phase, reason) =>
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

        _mockHttp.When("https://support.apple.com/en-us/102772")
            .Respond("text/html", appleHtml);

        var mockFactory = CreateMockHttpClientFactory();
        
        var testProducts = new List<Product>
        {
            new Product { Id = 1, Name = "iPhone 7 32GB", LifecyclePhase = "LEGACY", Sku = "IP7" }
        };

        _mockRepository.Setup(r => r.GetAllProductsAsync())
            .ReturnsAsync(testProducts);
        _mockRepository.Setup(r => r.UpdateProductPhaseAsync(1, "OBSOLETE", It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var sentinel = new LifecycleSentinel(_mockRepository.Object, mockFactory.Object);

        // Act
        await sentinel.RunDailyAuditAsync();

        // Assert
        _mockRepository.Verify(r => r.UpdateProductPhaseAsync(1, "OBSOLETE", It.IsAny<string>()), Times.Once);
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

        _mockHttp.When("https://support.apple.com/en-us/102772")
            .Respond("text/html", appleHtml);

        var mockFactory = CreateMockHttpClientFactory();
        
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

        var sentinel = new LifecycleSentinel(_mockRepository.Object, mockFactory.Object);

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

        _mockHttp.When("https://support.apple.com/en-us/102772")
            .Respond("text/html", appleHtml);

        var mockFactory = CreateMockHttpClientFactory();
        
        var testProducts = new List<Product>
        {
            new Product { Id = 1, Name = "iphone 14 pro", LifecyclePhase = "ACTIVE", Sku = "IP14P" }
        };

        _mockRepository.Setup(r => r.GetAllProductsAsync())
            .ReturnsAsync(testProducts);
        _mockRepository.Setup(r => r.UpdateProductPhaseAsync(It.IsAny<int>(), "LEGACY", It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var sentinel = new LifecycleSentinel(_mockRepository.Object, mockFactory.Object);

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

        _mockHttp.When("https://support.apple.com/en-us/102772")
            .Respond("text/html", appleHtml);

        var mockFactory = CreateMockHttpClientFactory();
        
        var testProducts = new List<Product>
        {
            new Product { Id = 1, Name = "Samsung Galaxy S21", LifecyclePhase = "ACTIVE", Sku = "SGS21" },
            new Product { Id = 2, Name = "Dell XPS 15", LifecyclePhase = "ACTIVE", Sku = "DXPS15" }
        };

        _mockRepository.Setup(r => r.GetAllProductsAsync())
            .ReturnsAsync(testProducts);

        var sentinel = new LifecycleSentinel(_mockRepository.Object, mockFactory.Object);

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
