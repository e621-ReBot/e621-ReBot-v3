using System;

namespace e621_ReBot_v3.Modules.Converter
{
    internal class Module_Converter
    {
        internal static void Report_Info(string InfoMessage)
        {
            Window_Main._RefHolder.Dispatcher.BeginInvoke(() =>
            {
                Window_Main._RefHolder.Convert_InfoTextBox.Text = $"{DateTime.Now.ToLongTimeString()}, {InfoMessage}\n{Window_Main._RefHolder.Convert_InfoTextBox.Text}";
            });
        }
    }
}