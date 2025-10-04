using System;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using FluentAssertions;
using GGs.Desktop.ViewModels;
using Xunit;

namespace GGs.Enterprise.Tests.UI;

/// <summary>
/// Comprehensive view-model unit tests ensuring proper initialization,
/// property change notifications, command wiring, and data binding support.
/// These tests validate that view-models work correctly without requiring
/// the full UI to be rendered, catching wiring issues early.
/// </summary>
public sealed class ViewModelTests
{
    [Fact]
    public void BaseViewModel_PropertyChanged_fires_when_property_changes()
    {
        // Arrange
        var vm = new TestViewModel();
        string? capturedPropertyName = null;
        vm.PropertyChanged += (s, e) => capturedPropertyName = e.PropertyName;

        // Act
        vm.TestProperty = "NewValue";

        // Assert
        capturedPropertyName.Should().Be(nameof(TestViewModel.TestProperty));
    }

    [Fact]
    public void BaseViewModel_PropertyChanged_does_not_fire_when_value_unchanged()
    {
        // Arrange
        var vm = new TestViewModel { TestProperty = "InitialValue" };
        int eventCount = 0;
        vm.PropertyChanged += (s, e) => eventCount++;

        // Act
        vm.TestProperty = "InitialValue"; // Same value

        // Assert
        eventCount.Should().Be(0, "PropertyChanged should not fire when value is unchanged");
    }

    [Fact]
    public void DashboardViewModel_initializes_all_commands()
    {
        // Arrange & Act
        var vm = new DashboardViewModel();

        // Assert
        vm.CreateTweakCommand.Should().NotBeNull("CreateTweakCommand should be initialized");
        vm.ManageUsersCommand.Should().NotBeNull("ManageUsersCommand should be initialized");
        vm.ViewAnalyticsCommand.Should().NotBeNull("ViewAnalyticsCommand should be initialized");
        vm.SystemHealthCommand.Should().NotBeNull("SystemHealthCommand should be initialized");
    }

    [Fact]
    public void DashboardViewModel_commands_can_execute()
    {
        // Arrange
        var vm = new DashboardViewModel();

        // Act & Assert - Commands should be executable (even if they do nothing without a window)
        vm.CreateTweakCommand.CanExecute(null).Should().BeTrue("CreateTweakCommand should be executable");
        vm.ManageUsersCommand.CanExecute(null).Should().BeTrue("ManageUsersCommand should be executable");
        vm.ViewAnalyticsCommand.CanExecute(null).Should().BeTrue("ViewAnalyticsCommand should be executable");
        vm.SystemHealthCommand.CanExecute(null).Should().BeTrue("SystemHealthCommand should be executable");
    }

    [Fact]
    public void NotificationsViewModel_initializes_without_errors()
    {
        // Arrange & Act
        Exception? capturedException = null;
        NotificationsViewModel? vm = null;

        try
        {
            vm = new NotificationsViewModel();
        }
        catch (Exception ex)
        {
            capturedException = ex;
        }

        // Assert
        capturedException.Should().BeNull("NotificationsViewModel should initialize without throwing");
        vm.Should().NotBeNull();
    }

    [Fact]
    public void NotificationsViewModel_implements_INotifyPropertyChanged()
    {
        // Arrange
        var vm = new NotificationsViewModel();

        // Assert
        vm.Should().BeAssignableTo<INotifyPropertyChanged>("NotificationsViewModel must support data binding");
    }

    [Fact]
    public void RelayCommand_executes_action_when_invoked()
    {
        // Arrange
        bool actionExecuted = false;
        var command = new RelayCommand(() => actionExecuted = true);

        // Act
        command.Execute(null);

        // Assert
        actionExecuted.Should().BeTrue("RelayCommand should execute the provided action");
    }

    [Fact]
    public void RelayCommand_respects_canExecute_predicate()
    {
        // Arrange
        bool canExecute = false;
        var command = new RelayCommand(() => { }, () => canExecute);

        // Act & Assert - Initially cannot execute
        command.CanExecute(null).Should().BeFalse("Command should respect canExecute predicate");

        // Change predicate result
        canExecute = true;
        command.CanExecute(null).Should().BeTrue("Command should reflect updated canExecute state");
    }

    [Fact]
    public void RelayCommand_CanExecuteChanged_event_can_be_subscribed()
    {
        // Arrange
        var command = new RelayCommand(() => { });
        Exception? capturedException = null;

        // Act - Subscribe to CanExecuteChanged event
        try
        {
            command.CanExecuteChanged += (s, e) => { /* Event handler */ };
        }
        catch (Exception ex)
        {
            capturedException = ex;
        }

        // Assert
        capturedException.Should().BeNull("CanExecuteChanged event should be available for subscription");
    }

    /// <summary>
    /// Test view-model for validating BaseViewModel behavior
    /// </summary>
    private class TestViewModel : BaseViewModel
    {
        private string _testProperty = string.Empty;

        public string TestProperty
        {
            get => _testProperty;
            set => SetField(ref _testProperty, value);
        }
    }
}

