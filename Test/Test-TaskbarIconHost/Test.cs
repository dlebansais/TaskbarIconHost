namespace TestTaskbarIconHost;

using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented

[TestClass]
public class Test
{
    [TestMethod]
    public void Test1()
    {
        using WindowsDriver Session = LaunchApp();

        AppiumElement ButtonNoElement = Session.FindElement(new ByName("Non"));
        ButtonNoElement.Click();

        StopApp();
    }

    private static WindowsDriver LaunchApp()
    {
        Thread.Sleep(TimeSpan.FromSeconds(10));

        AppiumOptions AppiumOptions = new();
        AppiumOptions.AddAdditionalAppiumOption("Application", @".\TaskbarIconHost\bin\x64\Debug\TaskbarIconHost.exe");
        AppiumOptions.AddAdditionalAppiumOption("ApplicationArguments", "bad");

        return new WindowsDriver(new Uri("http://127.0.0.1:4723"), AppiumOptions);
    }

    private static void StopApp() => Thread.Sleep(TimeSpan.FromSeconds(2));
}

#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

