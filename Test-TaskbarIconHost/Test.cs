namespace TestTaskbarIconHost
{
    using System;
    using System.Collections.ObjectModel;
    using System.Threading;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using OpenQA.Selenium;
    using OpenQA.Selenium.Appium;
    using OpenQA.Selenium.Appium.Windows;

    [TestClass]
    public class Test
    {
        [TestMethod]
        public void Test1()
        {
            WindowsDriver<WindowsElement> Session = LaunchApp();

            //WindowsElement ButtonNoElement = Session.FindElementByName("Non");
            //ButtonNoElement.Click();

            StopApp(Session);
        }

        private WindowsDriver<WindowsElement> LaunchApp()
        {
            Thread.Sleep(TimeSpan.FromSeconds(10));

            AppiumOptions AppiumOptions = new AppiumOptions();
            AppiumOptions.AddAdditionalCapability("app", @".\TaskbarIconHost\bin\x64\Debug\TaskbarIconHost.exe");
            AppiumOptions.AddAdditionalCapability("appArguments", "bad");

            return new WindowsDriver<WindowsElement>(new Uri("http://127.0.0.1:4723"), AppiumOptions);
        }

        private void StopApp(WindowsDriver<WindowsElement> session)
        {
            Thread.Sleep(TimeSpan.FromSeconds(2));
            using WindowsDriver<WindowsElement> DeletedSession = session;
        }
    }
}
