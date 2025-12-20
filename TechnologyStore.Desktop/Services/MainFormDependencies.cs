using TechnologyStore.Desktop.Config;
using TechnologyStore.Desktop.Services;
using TechnologyStore.Desktop.Features.Auth;
using TechnologyStore.Desktop.Features.Leave;
using TechnologyStore.Desktop.Features.Reporting;
using TechnologyStore.Desktop.Features.Products.Data;
using TechnologyStore.Desktop.Features.TimeTracking;
using TechnologyStore.Desktop.Features.Payroll;
using TechnologyStore.Shared.Interfaces;
using IOrderRepository = TechnologyStore.Shared.Interfaces.IOrderRepository;
using ISupplierRepository = TechnologyStore.Shared.Interfaces.ISupplierRepository;
using IPurchaseOrderService = TechnologyStore.Shared.Interfaces.IPurchaseOrderService;
// Resolve ambiguities favoring Desktop versions
using IProductRepository = TechnologyStore.Desktop.Features.Products.Data.IProductRepository;
using IAuthenticationService = TechnologyStore.Desktop.Features.Auth.IAuthenticationService;
using IUserRepository = TechnologyStore.Desktop.Features.Auth.IUserRepository;

namespace TechnologyStore.Desktop
{
    /// <summary>
    /// Aggregates repository dependencies to reduce parameter count.
    /// </summary>
    public class RepositoryDependencies
    {
        public IProductRepository ProductRepository { get; }
        public ILeaveRepository LeaveRepository { get; }
        public IOrderRepository OrderRepository { get; }
        public ISupplierRepository SupplierRepository { get; }

        public RepositoryDependencies(
            IProductRepository productRepository,
            ILeaveRepository leaveRepository,
            IOrderRepository orderRepository,
            ISupplierRepository supplierRepository)
        {
            ProductRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
            LeaveRepository = leaveRepository ?? throw new ArgumentNullException(nameof(leaveRepository));
            OrderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
            SupplierRepository = supplierRepository ?? throw new ArgumentNullException(nameof(supplierRepository));
        }
    }

    /// <summary>
    /// Aggregates dependencies required by <see cref="MainForm"/> into a single object.
    /// This reduces the constructor parameter count and keeps DI usage simple.
    /// </summary>
    public class MainFormDependencies
    {
        public IProductRepository Repository { get; }
        public IHealthCheckService HealthCheckService { get; }
        public IAuthenticationService AuthService { get; }
        public ILeaveRepository LeaveRepository { get; }
        public ISalesReportService SalesReportService { get; }
        public IOrderRepository OrderRepository { get; }
        public ISupplierRepository SupplierRepository { get; }
        public IPurchaseOrderService PurchaseOrderService { get; }
        public EmailSettings EmailSettings { get; }
        public UiSettings UiSettings { get; }
        public ApplicationSettings AppSettings { get; }

        public MainFormDependencies(
            RepositoryDependencies repositories,
            IHealthCheckService healthCheckService,
            IAuthenticationService authService,
            ISalesReportService salesReportService,
            IPurchaseOrderService purchaseOrderService,
            AppSettings rootSettings)
        {
            if (repositories == null) throw new ArgumentNullException(nameof(repositories));
            Repository = repositories.ProductRepository;
            LeaveRepository = repositories.LeaveRepository;
            OrderRepository = repositories.OrderRepository;
            SupplierRepository = repositories.SupplierRepository;

            HealthCheckService = healthCheckService ?? throw new ArgumentNullException(nameof(healthCheckService));
            AuthService = authService ?? throw new ArgumentNullException(nameof(authService));
            SalesReportService = salesReportService ?? throw new ArgumentNullException(nameof(salesReportService));
            PurchaseOrderService = purchaseOrderService ?? throw new ArgumentNullException(nameof(purchaseOrderService));

            if (rootSettings == null) throw new ArgumentNullException(nameof(rootSettings));
            EmailSettings = rootSettings.Email;
            UiSettings = rootSettings.Ui;
            AppSettings = rootSettings.Application;
        }

        // Time Tracking Dependencies (injected via property or constructor - adding via ctor)
        public ITimeTrackingService TimeTrackingService { get; set; }
        public IWorkShiftRepository WorkShiftRepository { get; set; }
        public IUserRepository UserRepository { get; set; }
        public IPayrollService PayrollService { get; set; }

        public void ConfigureTimeTracking(ITimeTrackingService timeTrackingService, IWorkShiftRepository workShiftRepository, IUserRepository userRepository, IPayrollService payrollService)
        {
            TimeTrackingService = timeTrackingService;
            WorkShiftRepository = workShiftRepository;
            UserRepository = userRepository;
            PayrollService = payrollService;
        }
    }
}
