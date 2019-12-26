/*
 * Copyright (C) 2019 russell brown
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
//using Winforms = System.Windows.Forms;
using System.Windows.Threading;

//
//  dotnet publish -r win-x64 -c Release /p:PublishSingleFile=true /p:PublishTrimmed=true
//

namespace imvert
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public partial class MainWindow : Window
    {
        private Processor processor = new Processor();
        private DispatcherTimer dt = new DispatcherTimer();
        private static MainWindow inst = null;
        public static MainWindow Inst { get => inst; }
        private static readonly Regex numRegex = new Regex("[^0-9]+"); //regex that matches disallowed text
        private static readonly string invalidFilenameChars = new string(System.IO.Path.GetInvalidFileNameChars());

        //
        // MainWindow
        //
        // WPF main window for ImVert
        //
        public MainWindow()
        {
            // Set up singleton
            inst = this;

            //l.To("imvert.log");

            // Use last used folder if set, else default to runtime dir
            if (Settings1.Default.lastFolder == null || Settings1.Default.lastFolder.Length == 0)
                Settings1.Default.lastFolder = AppDomain.CurrentDomain.BaseDirectory;

            // Initialize GUI
            InitializeComponent();
            limitSizeTB.IsEnabled = false;
            renameTB.IsEnabled = false;
            renameTB.Text = "";
            backupCB.IsChecked = true;
            limitSizeTB.Text = "";
            formatPD.Items.Add("No Change");
            formatPD.Items.Add("JPG");
            formatPD.Items.Add("PNG");
            formatPD.SelectedIndex = 0;// no change 
            useSubdirsCB.IsChecked = true;
            folderTB.Text = Settings1.Default.lastFolder;

            // Star thread for checking on background processor thread so gui runs smoothly
            dt.Tick += Dt_Tick;
            dt.Interval = new TimeSpan(1000000);
            dt.Start();
        }


        // limit size CB checked
        private void limitSizeCB_Click(object sender, RoutedEventArgs e)
        {
            limitSizeTB.IsEnabled = !limitSizeTB.IsEnabled;
        }

        // rename CB checked
        private void renameCB_Click(object sender, RoutedEventArgs e)
        {
            renameTB.IsEnabled = !renameTB.IsEnabled;
        }

        // limitSizeTB_PreviewTextInput
        //
        // text preview for limit size text box - only allow integers through

        private void limitSizeTB_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = numRegex.IsMatch(e.Text);
        }

        // renameTB_PreviewTextInput
        //
        //  text preview for rename text box - only allow valifd file characters through
        private void renameTB_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = invalidFilenameChars.Contains(e.Text);
        }

        // chooseFolderBTN_Click
        //
        // Select folder button clicked - display dialog box to choose folder
        private void chooseFolderBTN_Click(object sender, RoutedEventArgs e)
        {
            // NOTE - ookii dialog is used as WPF still dosn't have an inbuilt browser and 
            // we don't want to package up all of System.Windows.Forms to use that one
            var dlg = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();

            dlg.ShowNewFolderButton = false;
            dlg.SelectedPath = Settings1.Default.lastFolder;
            bool res = (bool)dlg.ShowDialog();
            if (res)
            {
                folderTB.Text = dlg.SelectedPath;
                Settings1.Default.lastFolder = folderTB.Text;
            }
        }

        // OnProcess
        //
        // User has clicked the 'process' button. Create a new background task to do the 
        // proessing ( stopping any one currently running )
        private void OnProcess(object sender, RoutedEventArgs e)
        {
            // if already processing, this is a cancel..
            if (processor.IsRunning())
            {
                MessageBoxResult res = MessageBox.Show("Processor is running, Cancel ?", "Warning", MessageBoxButton.YesNo);
                if (res == MessageBoxResult.Yes)
                    processor.StopTask();
                return;
            }
            // Here we go...
            // First do a little checking

            bool useSubDirs = (bool)useSubdirsCB.IsChecked;
            bool resize = (bool)limitSizeCB.IsChecked;
            bool rename = (bool)renameCB.IsChecked;
            bool backup = (bool)backupCB.IsChecked;
            string renameTxt = "";
            if (rename)
            {
                renameTxt = renameTB.Text;
                if (renameTxt.Length < 1)
                {
                    MessageBox.Show("No filename provided for rename", "Error", MessageBoxButton.OK);
                    return;
                }
            }
            string sourceDir = folderTB.Text;
            if (!Directory.Exists(sourceDir))
            {
                MessageBox.Show("Source folder does not exist", "Error", MessageBoxButton.OK);
                return;
            }
            int maxAxis = Common.NoResize;
            if (resize && limitSizeTB.Text.Length > 0)
            {
                bool ok = Int32.TryParse(limitSizeTB.Text, out maxAxis); // will work, only numeric allowed
                if (!ok || maxAxis < 5 || maxAxis > 32000)
                {
                    MessageBox.Show("Resize value " + maxAxis + " must be between 5 & 32000 ", "Error", MessageBoxButton.OK);
                    return;
                }

            }

            IsImageExtension.ImageType saveType = IsImageExtension.ImageType.NONE;
            switch (formatPD.SelectedIndex)
            {
                case 0: saveType = IsImageExtension.ImageType.NONE; break;
                case 1: saveType = IsImageExtension.ImageType.JPG; break;
                case 2: saveType = IsImageExtension.ImageType.PNG; break;
                default: break;
            }

            // All looks good...

            console.Items.Add("Processing");
            processor.AddTask(sourceDir, useSubDirs, maxAxis, renameTxt, saveType, backup);
            processBTN.Content = "Cancel";



        }


        private void Stopped()
        {
            processBTN.Content = "Process";
        }

        // Dt_Tick
        //
        // Timer callback, just display state of processor thread
        private void Dt_Tick(object sender, EventArgs e)
        {
            progressStr.Content = processor.GetStatus();
            // call 'Stopped' if it is finished to reset counters etc
            if (!processor.IsRunning())
                Stopped();
        }

        //Window_Closing
        //
        // Called when window is closing - save settings and terminate
        // processing thread.
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Settings1.Default.Save();
            processor.Close();
        }
    }
}
