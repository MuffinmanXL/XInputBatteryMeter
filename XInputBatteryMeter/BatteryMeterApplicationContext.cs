﻿using SharpDX.XInput;
using System;
using System.Linq;
using System.Windows.Forms;
using XInputBatteryMeter.Properties;

namespace XInputBatteryMeter
{
    public class BatteryMeterApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly BatteryStatusPoller _poller;
        private Controller _activeController;
#if DEBUG
        private const string _primaryUpdateUrl = "http://localhost:3000/xinput-battery-meter-update/update.xml";
#else
        private const string _primaryUpdateUrl = "https://matracey.github.io/xinput-battery-meter-update/update.xml";
#endif

        /// <summary>
        /// Creates a new BatteryMeterApplicationContext instance using the specified BatteryStatusPoller.
        /// </summary>
        /// <param name="poller">The BatteryStatusPoller that will query battery status.</param>
        public BatteryMeterApplicationContext(BatteryStatusPoller poller)
        {
            // Set up the poller values.
            _poller = poller;
            poller.ControllerConnected += Controller_Connected;
            poller.ControllerDisconnected += Controller_Disconnected;
            poller.ControllerBatteryLow += Controller_BatteryLow;
            poller.ControllerBatteryInformationUpdated += Controller_BatteryInformationUpdated;

            // Set up the Notification icon.
            _notifyIcon = new NotifyIcon
            {
                Icon = Resources.AppIcon,
                ContextMenu = new ContextMenu(),
                Visible = true,
                Text = Resources.AppName
            };

            _notifyIcon.ContextMenu.Popup += ContextMenu_Popup;

            foreach (var controller in _poller.Controllers)
            {
                var mainMenuItem = new MenuItem { Name = $"Controller{controller.UserIndex}_Main" };
                var batteryTypeMenuItem = new MenuItem { Enabled = false, Name = $"Controller{controller.UserIndex}_BatteryType" };
                var batteryLevelMenuItem = new MenuItem { Enabled = false, Name = $"Controller{controller.UserIndex}_BatteryLevel" };
                mainMenuItem.Click += MainMenuItem_Click;

                UpdateControllerStatus(controller, ref mainMenuItem, ref batteryTypeMenuItem, ref batteryLevelMenuItem);
                
                _notifyIcon.ContextMenu.MenuItems.Add(mainMenuItem);
                _notifyIcon.ContextMenu.MenuItems.Add(batteryTypeMenuItem);
                _notifyIcon.ContextMenu.MenuItems.Add(batteryLevelMenuItem);
                _notifyIcon.ContextMenu.MenuItems.Add("-");
            }

            var aboutItem = new MenuItem(Resources.AboutMenuItem, About_Clicked) { Name = "About" };
            var exitItem = new MenuItem(Resources.ExitMenuItem, Exit_Clicked) { Name = "Exit" };

            _notifyIcon.ContextMenu.MenuItems.Add(aboutItem);
            _notifyIcon.ContextMenu.MenuItems.Add(exitItem);

            UpdateActiveController();

            AutoUpdaterDotNET.AutoUpdater.Start(_primaryUpdateUrl);
        }

        /// <summary>
        /// Handles the Context Menu popup event, refreshing the battery information in each of the menu items.
        /// </summary>
        /// <param name="sender">The object that triggered this event.</param>
        /// <param name="e">The EventArgs.</param>
        private void ContextMenu_Popup(object sender, EventArgs e)
        {
            // Refresh the controller information.
            foreach (var controller in _poller.Controllers)
            {
                var menuItems = _notifyIcon.ContextMenu.MenuItems.Cast<MenuItem>();

                var mainMenuItem = menuItems.FirstOrDefault(x => x.Name.Equals($"Controller{controller.UserIndex}_Main"));
                var batteryTypeMenuItem = menuItems.FirstOrDefault(x => x.Name.Equals($"Controller{controller.UserIndex}_BatteryType"));
                var batteryLevelMenuItem = menuItems.FirstOrDefault(x => x.Name.Equals($"Controller{controller.UserIndex}_BatteryLevel"));

                UpdateControllerStatus(controller, ref mainMenuItem, ref batteryTypeMenuItem, ref batteryLevelMenuItem);
            }
        }

        /// <summary>
        /// Updates the specified menu item parameters with the latest Battery information for the specified Controller.
        /// </summary>
        /// <param name="controller">The controller to query Battery Information for.</param>
        /// <param name="mainMenuItem">The Main Menu Item for the controller.</param>
        /// <param name="batteryTypeMenuItem">The Battery Type Menu Item for the controller.</param>
        /// <param name="batteryLevelMenuItem">The Battery Level Menu Item for the controller.</param>
        private void UpdateControllerStatus(Controller controller, ref MenuItem mainMenuItem, ref MenuItem batteryTypeMenuItem, ref MenuItem batteryLevelMenuItem)
        {
            if (controller.IsConnected && _poller.ControllerBatteryInformation.ContainsKey(controller.UserIndex))
            {
                var batteryInfo = _poller.ControllerBatteryInformation[controller.UserIndex];
                mainMenuItem.Text = Resources.ControllerDescriptor + controller.UserIndex;
                batteryTypeMenuItem.Text = Resources.BatteryTypeDescriptor + batteryInfo.BatteryType;
                batteryLevelMenuItem.Text = Resources.BatteryLevelDescriptor + batteryInfo.BatteryLevel;
                mainMenuItem.Enabled = true;
                batteryTypeMenuItem.Visible = true;
                batteryLevelMenuItem.Visible = true;
            }
            else
            {
                mainMenuItem.Text = Resources.ControllerDescriptor + controller.UserIndex + Resources.NotConnected;
                mainMenuItem.Enabled = false;
                batteryTypeMenuItem.Visible = false;
                batteryLevelMenuItem.Visible = false;
            }
        }

        private void About_Clicked(object sender, EventArgs e)
        {
            var about = new AboutForm();
            about.Show();
        }

        private void Exit_Clicked(object sender, EventArgs e)
        {
            _notifyIcon.Visible = false;
            Application.Exit();
        }

        private void Controller_BatteryLow(object sender, UserIndexEventArgs e)
        {
            _notifyIcon.ShowBalloonTip(0, Resources.BatteryLowTitle, Resources.BatteryLowDescription.Replace(Resources.UserIndexPlaceholder, e.UserIndex.ToString()), ToolTipIcon.None);
        }

        private void Controller_Connected(object sender, UserIndexEventArgs e)
        {
            UpdateActiveController();
            _notifyIcon.ShowBalloonTip(0, Resources.ControllerConnectedTitle, Resources.ControllerConnectedDescription.Replace(Resources.UserIndexPlaceholder, e.UserIndex.ToString()), ToolTipIcon.None);
        }

        private void Controller_Disconnected(object sender, UserIndexEventArgs e)
        {
            UpdateActiveController();
            _notifyIcon.ShowBalloonTip(0, Resources.ControllerDisconnectedTitle, Resources.ControllerDisconnectedDescription.Replace(Resources.UserIndexPlaceholder, e.UserIndex.ToString()), ToolTipIcon.None);
        }

        private void Controller_BatteryInformationUpdated(object sender, UserIndexEventArgs e)
        {
            if (_activeController.UserIndex != e.UserIndex) return;
            UpdateAppTrayIcon(_poller.ControllerBatteryInformation[e.UserIndex]);
            UpdateAppTrayText(_poller.Controllers.FirstOrDefault(c => c.UserIndex == e.UserIndex), _poller.ControllerBatteryInformation[e.UserIndex]);
        }

        private void MainMenuItem_Click(object sender, EventArgs e)
        {
            var menuItem = (MenuItem)sender;
            UpdateActiveController(_poller.Controllers.FirstOrDefault(c => menuItem.Name == $"Controller{c.UserIndex}_Main"));
        }

        private void UpdateActiveController(Controller selectedController = null)
        {
            // Will be null if no controllers connected.
            if (selectedController == null) selectedController = _poller.Controllers.FirstOrDefault(c => c.IsConnected);

            if (selectedController != null)
            {
                if (_activeController != null && _activeController.UserIndex == selectedController.UserIndex) return;
                foreach (MenuItem item in _notifyIcon.ContextMenu.MenuItems) item.Checked = false;
                _activeController = selectedController;

                var menuItem = _notifyIcon.ContextMenu.MenuItems.Cast<MenuItem>().FirstOrDefault(x => x.Name.Equals($"Controller{selectedController.UserIndex}_Main"));
                if (menuItem != null) menuItem.Checked = true;

                if (!_poller.ControllerBatteryInformation.ContainsKey(selectedController.UserIndex)) return;
                UpdateAppTrayIcon(_poller.ControllerBatteryInformation[selectedController.UserIndex]);
                UpdateAppTrayText(selectedController, _poller.ControllerBatteryInformation[selectedController.UserIndex]);
            }
            else
            {
                foreach (MenuItem item in _notifyIcon.ContextMenu.MenuItems) item.Checked = false;
                _activeController = null;
                ResetAppTrayIcon();
            }
        }

        private void ResetAppTrayIcon()
        {
            _notifyIcon.Icon = Resources.AppIcon;
        }

        private void UpdateAppTrayText(Controller controller, BatteryInformation batteryInformation)
        {
            _notifyIcon.Text = Resources.NotificationIconText
                .Replace(Resources.UserIndexPlaceholder, controller.UserIndex.ToString())
                .Replace(Resources.BatteryTypePlaceholder, batteryInformation.BatteryType.ToString())
                .Replace(Resources.BatteryLevelPlaceholder, batteryInformation.BatteryLevel.ToString());
        }

        private void UpdateAppTrayIcon(BatteryInformation batteryInformation)
        {
            switch (batteryInformation.BatteryLevel)
            {
                case BatteryLevel.Empty:
                    _notifyIcon.Icon = Resources.batteryIcon_0;
                    break;
                case BatteryLevel.Low:
                    _notifyIcon.Icon = Resources.batteryIcon_33;
                    break;
                case BatteryLevel.Medium:
                    _notifyIcon.Icon = Resources.batteryIcon_66;
                    break;
                case BatteryLevel.Full:
                    _notifyIcon.Icon = Resources.batteryIcon_100;
                    break;
                default:
                    _notifyIcon.Icon = Resources.AppIcon;
                    break;
            }
        }
    }
}