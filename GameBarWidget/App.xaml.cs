using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Microsoft.Gaming.XboxGameBar;
using GameBarWidget.Services;

namespace GameBarWidget
{
    sealed partial class App : Application
    {
        private XboxGameBarWidget _widget1 = null;
        private XboxGameBarWidget _widget1Settings = null;

        public App()
        {
            this.InitializeComponent();
            this.UnhandledException += OnUnhandledException;
            this.Suspending += OnSuspending;
            _ = Task.Run(PoeItemParser.InitializeStatsDatabaseAsync);
        }

        private void OnUnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            System.Diagnostics.Debug.WriteLine($"[App] Prevented crash via Handled UnhandledException: {e.Message}");
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            if (rootFrame == null)
            {
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;
                Window.Current.Content = rootFrame;
            }

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    rootFrame.Navigate(typeof(Widget1), null);
                }
                Window.Current.Activate();
                LaunchDaemonIfAvailable();
            }
        }

        protected override void OnActivated(IActivatedEventArgs args)
        {
            XboxGameBarWidgetActivatedEventArgs widgetArgs = null;
            if (args.Kind == ActivationKind.Protocol)
            {
                var protocolArgs = args as IProtocolActivatedEventArgs;
                if (protocolArgs != null && protocolArgs.Uri.Scheme.Equals("ms-gamebarwidget", StringComparison.OrdinalIgnoreCase))
                {
                    widgetArgs = args as XboxGameBarWidgetActivatedEventArgs;
                }
            }

            if (widgetArgs == null)
            {
                widgetArgs = args as XboxGameBarWidgetActivatedEventArgs;
            }

            if (widgetArgs != null)
            {
                if (widgetArgs.IsLaunchActivation)
                {
                    var rootFrame = new Frame();
                    rootFrame.NavigationFailed += OnNavigationFailed;
                    Window.Current.Content = rootFrame;

                    // Route based on Game Bar AppExtensionId
                    if (string.Equals(widgetArgs.AppExtensionId, "Widget1Settings", StringComparison.OrdinalIgnoreCase))
                    {
                        _widget1Settings = new XboxGameBarWidget(
                            widgetArgs,
                            Window.Current.CoreWindow,
                            rootFrame);
                        rootFrame.Navigate(typeof(Widget1Settings), _widget1Settings);

                        Window.Current.Closed += (s, e) =>
                        {
                            _widget1Settings = null;
                            PoeSettingsManager.Instance.IsSettingsOpen = false;
                        };
                    }
                    else
                    {
                        _widget1 = new XboxGameBarWidget(
                            widgetArgs,
                            Window.Current.CoreWindow,
                            rootFrame);
                        rootFrame.Navigate(typeof(Widget1), _widget1);

                        Window.Current.Closed += async (s, e) =>
                        {
                            _widget1 = null;
                            await AppServiceManager.Instance.NotifyDaemonToCloseAsync();
                        };
                    }

                    Window.Current.Activate();
                    LaunchDaemonIfAvailable();
                }
            }
        }

        private async void LaunchDaemonIfAvailable()
        {
            try
            {
                await FullTrustLauncherHelper.LaunchDaemonAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auto-launch daemon info: {ex.Message}");
            }
        }

        protected override void OnBackgroundActivated(BackgroundActivatedEventArgs args)
        {
            base.OnBackgroundActivated(args);

            if (args.TaskInstance.TriggerDetails is AppServiceTriggerDetails details)
            {
                if (details.AppServiceConnection.AppServiceName == "CounterWidgetService" ||
                    details.AppServiceConnection.AppServiceName == "GameBarHotkeyService")
                {
                    AppServiceManager.Instance.RegisterConnection(
                        details.AppServiceConnection, 
                        args.TaskInstance.GetDeferral());
                }
            }
        }

        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        private async void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            await AppServiceManager.Instance.NotifyDaemonToCloseAsync();
            _widget1 = null;
            _widget1Settings = null;
            deferral.Complete();
        }
    }
}
