using e621_ReBot_v3.CustomControls;
using HtmlAgilityPack;
using System.Threading.Tasks;
using System.Windows;

namespace e621_ReBot_v3.Modules
{
    internal class Module_Tutorial
    {
        internal async static void Step_0()
        {
            await Task.Delay(3000); //Delay a bit to give time to update check first
            MessageBoxResult MessageBoxResultTemp = MessageBox.Show(Window_Main._RefHolder, "Since this is our first date, would you like to know more about me?", "e621 ReBot Tutorial", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
            if (MessageBoxResultTemp == MessageBoxResult.Yes)
            {
                Window_Main._RefHolder.ReBot_Menu_ListBox.SelectedIndex = 1;
                Window_Main._RefHolder.ListBoxItem_Browser_PreviewMouseLeftButtonDown(null, null);
            }
            else
            {
                AppSettings.FirstRun = false;
            }
        }

        internal static void Step_1()
        {
            MessageBox.Show(Window_Main._RefHolder, "Thanks for trying me out.\n\nFor a start, you should log in into e621 and provide me with API key so I could do the tasks you will require.\n\nI opened the login page for you.", "e621 ReBot Tutorial", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        internal static void Step_2()
        {
            MessageBox.Show(Window_Main._RefHolder, "I will grab some needed data from this page, then I'm going to point you to the next step.", "e621 ReBot Tutorial", MessageBoxButton.OK, MessageBoxImage.Information);

            HtmlDocument HtmlDocumentTemp = new HtmlDocument();
            HtmlDocumentTemp.LoadHtml(Module_CefSharp.BrowserHTMLSource);

            string MetaDataNode = HtmlDocumentTemp.DocumentNode.SelectSingleNode(".//body[@data-user-name]").Attributes["data-user-name"].Value;
            AppSettings.UserName = MetaDataNode;
            MetaDataNode = HtmlDocumentTemp.DocumentNode.SelectSingleNode(".//body[@data-user-id]").Attributes["data-user-id"].Value;
            AppSettings.UserID = MetaDataNode;
            Module_CefSharp.LoadURL($"https://e621.net/api_keys");
        }

        internal static void Step_3(bool isTutorial = false)
        {
            MessageBox.Show(Window_Main._RefHolder, "Generate you API key and copy it into the floating box.", $"e621 ReBot{(isTutorial ? " Tutorial" : null)}", MessageBoxButton.OK, MessageBoxImage.Information);
            if (Window_APIKey._RefHolder == null) new Window_APIKey().Show();
        }

        internal static void Step_Last()
        {
            MessageBox.Show(Window_Main._RefHolder, "You can change or remove your API key at any time in the settings.", "e621 ReBot Tutorial", MessageBoxButton.OK, MessageBoxImage.Information);
            AppSettings.FirstRun = false;
            AppSettings.SaveSettings();
            MessageBox.Show(Window_Main._RefHolder, "You can now go visit your favorite artists' galeries and grab your favorite media (downloading from e621.net is also supported).\nList of supported sites will be shown under this message box.\n\nWhen browsing a supported site, buttons will appear in the upper right corner. You just need to decide if you wish to download media straight away or grab it for later use.\nGrabbed media will be stored in the Grid tab.\n\nRead button tooltips for further explanations on how things work.", "e621 ReBot Tutorial", MessageBoxButton.OK, MessageBoxImage.Information);
            BrowserControl._RefHolder.BrowserQuickButtons.IsEnabled = true;
        }
    }
}