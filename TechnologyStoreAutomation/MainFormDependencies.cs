using TechnologyStoreAutomation.backend.auth;
using TechnologyStoreAutomation.backend.leave;
using TechnologyStoreAutomation.backend.reporting;
using TechnologyStoreAutomation.backend.trendCalculator.data;

namespace TechnologyStoreAutomation
{
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
        public EmailSettings EmailSettings { get; }
        public UiSettings UiSettings { get; }
        public ApplicationSettings AppSettings { get; }

        public MainFormDependencies(
            IProductRepository repository,
            IHealthCheckService healthCheckService,
            IAuthenticationService authService,
            ILeaveRepository leaveRepository,
            ISalesReportService salesReportService,
            AppSettings rootSettings)
        {
            Repository = repository ?? throw new ArgumentNullException(nameof(repository));
            HealthCheckService = healthCheckService ?? throw new ArgumentNullException(nameof(healthCheckService));
            AuthService = authService ?? throw new ArgumentNullException(nameof(authService));
            LeaveRepository = leaveRepository ?? throw new ArgumentNullException(nameof(leaveRepository));
            SalesReportService = salesReportService ?? throw new ArgumentNullException(nameof(salesReportService));

            if (rootSettings == null) throw new ArgumentNullException(nameof(rootSettings));
            EmailSettings = rootSettings.Email;
            UiSettings = rootSettings.Ui;
            AppSettings = rootSettings.Application;
        }
    }
}
