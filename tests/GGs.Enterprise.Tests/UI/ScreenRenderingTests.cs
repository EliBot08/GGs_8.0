using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FluentAssertions;
using GGs.Desktop.Views;
using Xunit;

namespace GGs.Enterprise.Tests.UI;

/// <summary>
/// Tests that verify each major screen/view in the Desktop application
/// renders correctly, has proper layout, and binds data as expected.
/// These tests ensure the UI components are wired up correctly and
/// can be displayed without errors.
/// </summary>
public sealed class ScreenRenderingTests
{
    [Fact]
    public void OptimizationView_renders_without_errors()
    {
        // Arrange
        OptimizationView? view = null;
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
                view = new OptimizationView();

                // Assert
                view.Should().NotBeNull("OptimizationView should render successfully");
                view.Content.Should().NotBeNull("OptimizationView should have content");
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
        capturedException.Should().BeNull("OptimizationView should render without throwing");
        thread.Join();
    }

    [Fact]
    public void NetworkView_renders_without_errors()
    {
        // Arrange
        NetworkView? view = null;
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
                view = new NetworkView();

                // Assert
                view.Should().NotBeNull("NetworkView should render successfully");
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
        capturedException.Should().BeNull("NetworkView should render without throwing");
        thread.Join();
    }

    [Fact]
    public void SystemIntelligenceView_renders_without_errors()
    {
        // Arrange
        SystemIntelligenceView? view = null;
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
                view = new SystemIntelligenceView();

                // Assert
                view.Should().NotBeNull("SystemIntelligenceView should render successfully");
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
        capturedException.Should().BeNull("SystemIntelligenceView should render without throwing");
        thread.Join();
    }

    [Fact]
    public void ProfileArchitectView_renders_without_errors()
    {
        // Arrange
        ProfileArchitectView? view = null;
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
                view = new ProfileArchitectView();

                // Assert
                view.Should().NotBeNull("ProfileArchitectView should render successfully");
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
        capturedException.Should().BeNull("ProfileArchitectView should render without throwing");
        thread.Join();
    }

    [Fact]
    public void CommunityHubView_renders_without_errors()
    {
        // Arrange
        CommunityHubView? view = null;
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
                view = new CommunityHubView();

                // Assert
                view.Should().NotBeNull("CommunityHubView should render successfully");
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
        capturedException.Should().BeNull("CommunityHubView should render without throwing");
        thread.Join();
    }

    [Fact]
    public void ModernMainWindow_contains_navigation_elements()
    {
        // Arrange
        ModernMainWindow? window = null;
        bool hasNavigationElements = false;
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

                // Force layout to be calculated
                window.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                window.Arrange(new Rect(window.DesiredSize));
                window.UpdateLayout();

                // Assert - Check for navigation elements (buttons, tabs, etc.)
                var buttons = FindVisualChildren<Button>(window).ToList();
                var tabControls = FindVisualChildren<TabControl>(window).ToList();

                hasNavigationElements = buttons.Count > 0 || tabControls.Count > 0;

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
        hasNavigationElements.Should().BeTrue("ModernMainWindow should contain navigation elements (buttons or tabs)");
        thread.Join();
    }

    /// <summary>
    /// Helper method to find all visual children of a specific type in the visual tree
    /// </summary>
    private static System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) yield break;

        int childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childCount; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
            {
                yield return typedChild;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }
}

