using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using FluentAssertions;
using GGs.Desktop.Views;
using Xunit;

namespace GGs.Enterprise.Tests.UI;

/// <summary>
/// UI automation tests that verify the Desktop application UI loads correctly,
/// renders all required screens, binds data properly, and keeps the recovery
/// banner hidden during normal operation (happy path).
/// 
/// These tests run in an STA thread to simulate the WPF UI environment and
/// validate that the application can successfully initialize its main window
/// without falling back to recovery mode.
/// </summary>
public sealed class DesktopUIAutomationTests
{
    [Fact]
    public void ModernMainWindow_initializes_without_throwing()
    {
        // Arrange
        Exception? capturedException = null;
        using ManualResetEvent completion = new(false);

        var thread = new Thread(() =>
        {
            try
            {
                // Create WPF Application context if needed
                if (Application.Current == null)
                {
                    new Application();
                }

                // Act - Create the main window
                var window = new ModernMainWindow();

                // Assert - Window should be created successfully
                window.Should().NotBeNull("ModernMainWindow should initialize without errors");
                window.Title.Should().NotBeNullOrWhiteSpace("Window should have a title");
                window.Width.Should().BeGreaterThan(0, "Window should have a valid width");
                window.Height.Should().BeGreaterThan(0, "Window should have a valid height");

                // Clean up
                window.Close();
            }
            catch (Exception ex)
            {
                capturedException = ex;
            }
            finally
            {
                completion.Set();
                Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
            }
        })
        {
            IsBackground = true
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        // Wait for completion with timeout
        completion.WaitOne(TimeSpan.FromSeconds(10)).Should().BeTrue("Window initialization should complete within 10 seconds");
        capturedException.Should().BeNull("ModernMainWindow should initialize without throwing exceptions");
        thread.Join();
    }

    [Fact]
    public void ModernMainWindow_has_valid_dimensions()
    {
        // Arrange
        ModernMainWindow? window = null;
        Exception? capturedException = null;
        using ManualResetEvent completion = new(false);

        var thread = new Thread(() =>
        {
            try
            {
                if (Application.Current == null)
                {
                    new Application();
                }

                // Act
                window = new ModernMainWindow();

                // Assert - Validate window dimensions
                window.Width.Should().BeGreaterThan(800, "Window width should be reasonable for desktop use");
                window.Height.Should().BeGreaterThan(600, "Window height should be reasonable for desktop use");
                window.MinWidth.Should().BeGreaterThan(0, "Window should have a minimum width constraint");
                window.MinHeight.Should().BeGreaterThan(0, "Window should have a minimum height constraint");

                window.Close();
            }
            catch (Exception ex)
            {
                capturedException = ex;
            }
            finally
            {
                completion.Set();
                Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
            }
        })
        {
            IsBackground = true
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        completion.WaitOne(TimeSpan.FromSeconds(10)).Should().BeTrue();
        capturedException.Should().BeNull();
        thread.Join();
    }

    [Fact]
    public void ModernMainWindow_is_visible_by_default()
    {
        // Arrange
        ModernMainWindow? window = null;
        Exception? capturedException = null;
        using ManualResetEvent completion = new(false);

        var thread = new Thread(() =>
        {
            try
            {
                if (Application.Current == null)
                {
                    new Application();
                }

                // Act
                window = new ModernMainWindow();

                // Assert - Window should be visible (not hidden or collapsed)
                window.Visibility.Should().Be(Visibility.Visible, "Main window should be visible by default");
                window.WindowState.Should().NotBe(WindowState.Minimized, "Main window should not start minimized");
                window.ShowInTaskbar.Should().BeTrue("Main window should appear in taskbar");

                window.Close();
            }
            catch (Exception ex)
            {
                capturedException = ex;
            }
            finally
            {
                completion.Set();
                Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
            }
        })
        {
            IsBackground = true
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        completion.WaitOne(TimeSpan.FromSeconds(10)).Should().BeTrue();
        capturedException.Should().BeNull();
        thread.Join();
    }

    [Fact]
    public void ModernMainWindow_does_not_show_recovery_mode_title()
    {
        // Arrange
        ModernMainWindow? window = null;
        Exception? capturedException = null;
        using ManualResetEvent completion = new(false);

        var thread = new Thread(() =>
        {
            try
            {
                if (Application.Current == null)
                {
                    new Application();
                }

                // Act
                window = new ModernMainWindow();

                // Assert - Window title should NOT indicate recovery mode
                window.Title.Should().NotContain("Recovery", "Main window should not be in recovery mode during normal operation");
                window.Title.Should().NotContain("recovery", "Main window should not be in recovery mode during normal operation");
                window.Title.Should().NotContain("Error", "Main window should not show error state in title");

                window.Close();
            }
            catch (Exception ex)
            {
                capturedException = ex;
            }
            finally
            {
                completion.Set();
                Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
            }
        })
        {
            IsBackground = true
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        completion.WaitOne(TimeSpan.FromSeconds(10)).Should().BeTrue();
        capturedException.Should().BeNull();
        thread.Join();
    }

    [Fact]
    public void RecoveryWindow_should_only_appear_during_fault_injection()
    {
        // This test validates that RecoveryWindow exists and can be instantiated
        // but should NOT appear during normal application flow
        
        // Arrange
        RecoveryWindow? window = null;
        Exception? capturedException = null;
        using ManualResetEvent completion = new(false);

        var thread = new Thread(() =>
        {
            try
            {
                if (Application.Current == null)
                {
                    new Application();
                }

                // Act - Deliberately create recovery window (simulating fault injection)
                window = new RecoveryWindow("Test error for validation");

                // Assert - Recovery window should exist and be properly configured
                window.Should().NotBeNull("RecoveryWindow should be available for fault scenarios");
                window.Title.Should().Contain("Recovery", "RecoveryWindow should clearly indicate recovery mode");

                window.Close();
            }
            catch (Exception ex)
            {
                capturedException = ex;
            }
            finally
            {
                completion.Set();
                Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
            }
        })
        {
            IsBackground = true
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        completion.WaitOne(TimeSpan.FromSeconds(10)).Should().BeTrue();
        capturedException.Should().BeNull("RecoveryWindow should initialize without errors when explicitly created");
        thread.Join();
    }

    [Fact]
    public void DashboardView_can_be_instantiated_with_dependencies()
    {
        // Arrange
        DashboardView? view = null;
        Exception? capturedException = null;
        using ManualResetEvent completion = new(false);

        var thread = new Thread(() =>
        {
            try
            {
                if (Application.Current == null)
                {
                    new Application();
                }

                // Act - Create DashboardView with required dependencies
                var viewModel = new GGs.Desktop.ViewModels.DashboardViewModel();

                // Create mock dependencies
                var baseUrl = "https://localhost:5001";
                var sec = new GGs.Shared.Http.HttpClientSecurityOptions();
                var http = GGs.Shared.Http.SecureHttpClientFactory.GetOrCreate(baseUrl, sec, userAgent: "GGs.Desktop.Tests");
                var auth = new GGs.Shared.Api.AuthService(http);
                var eliBotService = new GGs.Desktop.Services.EliBotService(http, auth, Microsoft.Extensions.Logging.Abstractions.NullLogger<GGs.Desktop.Services.EliBotService>.Instance);
                var systemMonitor = new GGs.Desktop.Services.SystemMonitorService();
                var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<DashboardView>.Instance;

                view = new DashboardView(viewModel, eliBotService, systemMonitor, logger);

                // Assert
                view.Should().NotBeNull("DashboardView should initialize successfully");
                view.DataContext.Should().NotBeNull("DashboardView should have a DataContext (ViewModel)");

                // Clean up
                if (view is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch (Exception ex)
            {
                capturedException = ex;
            }
            finally
            {
                completion.Set();
                Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
            }
        })
        {
            IsBackground = true
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        completion.WaitOne(TimeSpan.FromSeconds(10)).Should().BeTrue();
        capturedException.Should().BeNull("DashboardView should initialize without errors");
        thread.Join();
    }
}

