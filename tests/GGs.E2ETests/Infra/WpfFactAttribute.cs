using System;
using Xunit;

namespace GGs.E2ETests.Infra
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class WpfFactAttribute : FactAttribute { }
}

