using System;
using System.Text;
using avaness.PluginLoader.Stats;
using Sandbox.Graphics.GUI;
using VRageMath;

namespace avaness.PluginLoader.GUI
{
    public static class PlayerConsent
    {
        public static event Action OnConsentChanged;

        public static void ShowDialog(Action continuation = null)
        {
            MyGuiScreenMessageBox dialog = MyGuiSandbox.CreateMessageBox(buttonType: MyMessageBoxButtonsType.YES_NO_CANCEL,
                    messageText: new StringBuilder(
                        "               Would you like to rate plugins and inform developers?\r\n" +
                        "\r\n" +
                        "\r\n" +
                        "YES: Plugin Loader will send the list of enabled plugins to our server\r\n" +
                        "         each time the game starts. Your Steam ID is sent only in hashed form,\r\n" +
                        "         which makes it hard to identify you. Plugin usage statistics is kept\r\n" +
                        "         for up to 90 days. Votes on plugins are preserved indefinitely.\r\n" +
                        "         Server log files and database backups may be kept up to 90 days.\r\n" +
                        "         Location of data storage: European Union\r\n" +
                        "\r\n" +
                        "\r\n" +
                        "NO:   None of your data will be sent to nor stored on our statistics server.\r\n" +
                        "         Plugin Loader will still connect to download the statistics shown.\r\n"),
                    size: new Vector2(0.6f, 0.6f),
                    messageCaption: new StringBuilder("Consent"),
                    callback: result => GetConfirmation(result, continuation));

            if (dialog.Controls.GetControlByName("MyGuiControlMultilineText") is MyGuiControlMultilineText text)
                text.TextAlign = VRage.Utils.MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER;

            MyGuiSandbox.AddScreen(dialog);
        }

        public static bool ConsentRequested => !string.IsNullOrEmpty(Main.Instance.Config.DataHandlingConsentDate);

        public static bool ConsentGiven => Main.Instance.Config.DataHandlingConsent;

        private static void GetConfirmation(MyGuiScreenMessageBox.ResultEnum result, Action continuation)
        {
            if (result == MyGuiScreenMessageBox.ResultEnum.CANCEL)
                return;

            var consent = result == MyGuiScreenMessageBox.ResultEnum.YES;

            var consentWithdrawn = ConsentRequested && ConsentGiven && !consent;
            if (consentWithdrawn)
            {
                MyGuiSandbox.AddScreen(MyGuiSandbox.CreateMessageBox(MyMessageBoxStyleEnum.Info, MyMessageBoxButtonsType.YES_NO_CANCEL, new StringBuilder("Are you sure to withdraw your consent to data handling?\r\n\r\nDoing so would irrecoverably remove all your votes\r\nand usage data from our statistics server."), new StringBuilder("Confirm consent withdrawal"), callback: res => StoreConsent(res, false, continuation)));
                return;
            }

            StoreConsent(MyGuiScreenMessageBox.ResultEnum.YES, consent, continuation);
        }

        private static void StoreConsent(MyGuiScreenMessageBox.ResultEnum confirmationResult, bool consent, Action continuation)
        {
            if (confirmationResult != MyGuiScreenMessageBox.ResultEnum.YES)
                return;

            if (ConsentRequested && consent == ConsentGiven)
            {
                continuation?.Invoke();
                return;
            }

            if (!StatsClient.Consent(consent))
            {
                LogFile.Error("Failed to register player consent on statistics server");
                return;
            }

            var config = Main.Instance.Config;
            config.DataHandlingConsentDate = Tools.Tools.FormatDateIso8601(DateTime.Today);
            config.DataHandlingConsent = consent;
            config.Save();

            if (consent)
                StatsClient.Track(Main.Instance.TrackablePluginIds);

            OnConsentChanged?.Invoke();

            continuation?.Invoke();
        }
    }
}