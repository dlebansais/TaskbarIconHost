namespace TestTaskbarIconHost
{
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
            /*
            WindowsDriver<WindowsElement> Session = LaunchApp();

            WindowsElement ButtonNoElement = Session.FindElementByName("Non");
            ButtonNoElement.Click();

            StopApp(Session);
            */
        }

        private static WindowsDriver<WindowsElement> LaunchApp()
        {
            Thread.Sleep(TimeSpan.FromSeconds(10));

            AppiumOptions AppiumOptions = new AppiumOptions();
            AppiumOptions.AddAdditionalCapability("app", @".\TaskbarIconHost\bin\x64\Debug\TaskbarIconHost.exe");
            AppiumOptions.AddAdditionalCapability("appArguments", "bad");

            return new WindowsDriver<WindowsElement>(new Uri("http://127.0.0.1:4723"), AppiumOptions);
        }

        private static void StopApp(WindowsDriver<WindowsElement> session)
        {
            Thread.Sleep(TimeSpan.FromSeconds(2));
            using WindowsDriver<WindowsElement> DeletedSession = session;
        }
    }

#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
