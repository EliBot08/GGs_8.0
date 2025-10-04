using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using FluentAssertions;
using Xunit;

namespace GGs.Enterprise.Tests.UI;

public sealed class EnterpriseControlStylesSmokeTests
{
    [Fact]
    public void EnterpriseControlStyles_dictionary_loads_without_animation_freeze()
    {
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

                var uri = new Uri("/GGs.Desktop;component/Themes/EnterpriseControlStyles.xaml", UriKind.Relative);
                var dictionary = (ResourceDictionary)Application.LoadComponent(uri);
                dictionary.Should().NotBeNull();
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

        completion.WaitOne(TimeSpan.FromSeconds(5)).Should().BeTrue("resource dictionaries should load quickly");
        capturedException.Should().BeNull("EnterpriseControlStyles.xaml should load without storyboard freeze errors");
        thread.Join();
    }
}
