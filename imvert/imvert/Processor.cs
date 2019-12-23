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
using System.Threading;

namespace imvert
{
    //
    // Pocessor
    //
    // This class performs the conversion of all the relevant images in a folder and 
    // optionally sub folders.
    // It is created at startup, and performs the conversion task when 'AddTask' is called.
    // Conversion is done in a background thread and can be cancelled by calling 'Stop'
    // The main task can check on the state by calling 'GetStatus' or 'IsRunning'
    class Processor
    {
        private string sourceDir;       // The source ( top ) folder
        private bool useSubDirs;        // Indicates sub folders should be processed
        private bool backup = true;     // Indicates a backup should be made of any file to be modified
        private int maxAxis;            // The max length of either axis or NoResize
        private int filesCounted = 0;   // Files processed
        private int dirsCounted = 0;    // Folders processed
        private int imageFiles = 0;     // Image files processed
        private string renameTxt;       // If set, used to rename files with his as a prefix
        private Thread t;               // The background thread 
        private bool isRunning = false; // If set, the background thread is running
        private bool cancelTask = false;// If set, indicates to background thread that we want to stop
        private IsImageExtension.ImageType saveType = IsImageExtension.ImageType.NONE;
                                        // If not 'NONE' then this indicates the file type to convert to
 
        public  Processor()
        {
        }

        //
        // AddTask
        //
        // Called from main windod to start processing with prodided parameters
        //
        public void AddTask(string sourceDir, bool useSubDirs, int maxAxis, string renameTxt, IsImageExtension.ImageType saveType, bool backup)
        {
            l.Info("Add task dir:{0}, subs:{1}, max:{2}, as:{3}", sourceDir, useSubDirs ? "Yes" : "no",  maxAxis,  saveType);
            this.sourceDir = sourceDir;
            this.useSubDirs = useSubDirs;
            this.maxAxis = maxAxis;
            this.renameTxt = renameTxt;
            this.saveType = saveType;
            this.backup = backup;
            dirsCounted = 0;
            filesCounted = 0;
            imageFiles = 0;
            isRunning = true;
            cancelTask = false;
            // If a thread was running, join it to end cleanly
            if (t != null)
                t.Join();
            // start a new thread to do the actual processing
            t = new Thread(new ThreadStart(fileWalkerThread));
            t.Start();
        }

        //
        // StopTask
        //
        // Called by mainwindow to cancel the processing task. 
        //
        public void StopTask()
        {
            // set cancelTask - this is picked up by background thread and it will stop
            cancelTask = true;
        }

        //
        // GetStatus
        //
        // Called by mainwindow to get status of the task. 
        //      
        public string GetStatus()
        {
            return (isRunning ? "Running " : "Finished ") + "Dirs:" + dirsCounted.ToString() + ", Files:" + filesCounted.ToString();
        }

        //
        // IsRunning
        //
        // Called by mainwindow to check if processing task has finished. 
        //       
        public bool IsRunning()
        {
            return isRunning;
        }


        //
        // Close
        //
        // Called by main window when we are exiting app 
        //           
        public void Close()
        {
            isRunning = false;
            // stop any running task
            cancelTask = true;
            if (t != null)
                t.Join();            
        }

        //
        // fileWalkerThread
        //
        // Forms the background thread 
        //    
        private void fileWalkerThread()
        {
            // Get top level director info and call walk to process it ( this is recursive if needed )
            DirectoryInfo dtop = new DirectoryInfo(sourceDir);
            walk(dtop);
            // Get here when finished. Set isRunning to false to indicate we are done
            isRunning = false;
        }


        //
        // processFile
        //
        // process an individual file. 
        //    
        private void processFile(FileInfo finfo)
        {
            filesCounted++;

            // form a new name if needed
            string renameTo = "";
            if ( renameTxt.Length > 0 )
            {
                renameTo = renameTxt + imageFiles.ToString();
            }
            // do image processing - ensure we cleanup after each image
            using (ProcessFile pf = new ProcessFile())
            {
                // returns true if processed correctly
                if (pf.Process(finfo, maxAxis, renameTo, saveType, backup))
                    imageFiles++;
            }
        }

        //
        // processFolder
        //
        // process a folder. 
        //    
        private void processFolder(DirectoryInfo dinfo)
        {
            dirsCounted++;
        }

        //
        // walk
        //
        // iterate over all files & folders in a folder calling 
        // processFile or processFolder as needed.
        // Call ourcelves recursivly to process sub folders
        //    
        private void walk(DirectoryInfo root)
        {
            System.IO.FileInfo[] files = null;
            System.IO.DirectoryInfo[] subDirs = null;

            // Don't process any of our backup folders
            if (root.Name == Common.BackupDir )
                return;

            try
            {
                // process this folder
                processFolder(root);

                // then process all the files in this folder
                files = root.GetFiles("*.*");

                if (files != null)
                {
                    foreach (System.IO.FileInfo fi in files)
                        if (!cancelTask)
                            processFile(fi);
                        else
                            return;
                }

                // then any sub folders if needed
                if (useSubDirs)
                {
                    subDirs = root.GetDirectories();
                    foreach (System.IO.DirectoryInfo dirInfo in subDirs)
                        if (!cancelTask)
                            walk(dirInfo);
                        else
                            return;
                }
            }
            catch (UnauthorizedAccessException e)
            {
                
            }
            catch (System.IO.DirectoryNotFoundException e)
            {
                
            }
        }
    }

}
