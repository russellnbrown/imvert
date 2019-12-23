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
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace imvert
{
    //
    // ProcessFile
    // 
    // Process an individual file applying all tranformations. Implement IDisposable to
    // ensure timely cleanup of resources
    //
    class ProcessFile : IDisposable

    {
        private Stream s = null;    // Read the image from a stream
        private Image  i = null;     // The image

        //
        // Dispose
        //
        // Cleanup any resources
        //
        public void Dispose()
        {
            if (i != null)
                i.Dispose();
        }

        //
        // Process
        //
        // Perform the transformations
        //
        public bool Process(FileInfo infile, int maxAxis, string renameTxt, IsImageExtension.ImageType saveType, bool backup)
        {
            // dont attempt to open a file if it dosn't have a correct extension
            if (!IsImageExtension.IsImageFileExtension(infile))
                return false;

            // save original name & open the file as a stream
            string oldFile = infile.FullName;
            s = File.OpenRead(infile.FullName);

            // determin type from file contents, if not an image file, close and return
            IsImageExtension.ImageType itype;
            if (!IsImageExtension.IsImage(s, out itype))
            {
                s.Close();
                return false;
            }

            // Looks like a valid image file, read it and close stream
            l.Info("Processing {0}", infile.FullName);
            i =  Image.FromStream(s);
            s.Close();

            // needNew will be set if we do anything that requires the file to be saved
            bool needNew = false;

            // if no change in format requested, use existing format
            if (saveType == IsImageExtension.ImageType.NONE)
                saveType = itype;

            // if save format is different, then set needNew so it gets saved
            if (saveType != itype)
            {
                needNew = true;
                l.Info("\tfile type changed");
                string act = String.Format("File " + infile.Name + " converted to " + saveType.ToString());
                l.Info("\t" + act);
                Common.ConsoleWrite(act);

            }

            // do we need to resize the image ?
            if (maxAxis != Common.NoResize && (i.Width > maxAxis || i.Height > maxAxis))
            {
                // yes - set needNew & calculate ratio and new lengths
                needNew = true;
                double ratioX = (double)maxAxis / (double)i.Width;
                double ratioY = (double)maxAxis / (double)i.Height;
                double ratio = ratioX < ratioY ? ratioX : ratioY;
                int newHeight = Convert.ToInt32(i.Height * ratio);
                int newWidth = Convert.ToInt32(i.Width * ratio);
                string act = String.Format("File " + infile.Name + " reduced from {0},{1} to {2},{3}", i.Width, i.Height, newWidth, newHeight);
                l.Info("\t" + act);
                Common.ConsoleWrite(act);
                Bitmap newi = new Bitmap(i, newWidth, newHeight);
                i.Dispose();
                i = newi;
            }

            // if we dont need to save this file, we may still need to rename it
            if (!needNew)
            {
                if (renameTxt.Length > 0)
                {
                    // if so the we can just move to the new filename. backup first if requested
                    if (backup)
                        backupFile(infile);
                    string newFileName = infile.DirectoryName + "\\" + renameTxt + infile.Extension;
                    l.Info("\tRenaming {0} to {1}", infile.FullName, newFileName);
                    File.Move(infile.FullName, newFileName);
                    Common.ConsoleWrite("Renaming " + infile.FullName + " to " + newFileName);
                    return true;
                }
                else
                {
                    // nothing to do
                    l.Info("\tNo change needed");
                    return false;
                }
            }

            // If we get here then we will be writing a new file. backup old if requested
            if (backup)
                backupFile(infile);
            File.Delete(oldFile);
            Save(infile, i, saveType, renameTxt);

            return true;
        }

        //
        // backupFile
        //
        // Copies a file to be transformed into a backup directory
        //
        void backupFile(FileInfo infile)
        {
            // Check if folder exists, create if not
            string backupDir = infile.DirectoryName + "\\" + Common.BackupDir;
            if (!Directory.Exists(backupDir))
                Directory.CreateDirectory(backupDir);

            // now copy, prevent overriding incase run multiple times
            l.Info("\tbacking up to " + backupDir + "\\" + infile.Name);
            try
            {
                File.Copy(infile.FullName, backupDir + "\\" + infile.Name, false);
            }
            catch (Exception e)
            {
                l.Warn("Move of " + infile.FullName + " failed as it has already been done");
            }
        }

        //
        // Save
        //
        // Save the new file in requested format
        //
        internal void Save(FileInfo orig, Image i, IsImageExtension.ImageType saveType, string renameTxt)
        {
            //
            // Create the new files name taking account of any renaming of change of type
            string dir = orig.DirectoryName;
            string name = orig.Name;
            string ext = orig.Extension;
            string newExt = "." + saveType.ToString().ToLower();
            if (renameTxt.Length > 0)
                name = renameTxt + newExt;
            else
            {
                name = name.Substring(0, name.Length - ext.Length);
                name = name + newExt;
            }
            FileInfo newFile = new FileInfo(dir + "\\" + name);
            l.Info("\tSaving as {0} ", newFile.FullName);

            //
            // save the file in correct format. Jpeg also has quality value
            if (saveType == IsImageExtension.ImageType.JPG)
            {
                ImageCodecInfo myImageCodecInfo;
                Encoder myEncoder;
                EncoderParameter myEncoderParameter;
                EncoderParameters myEncoderParameters;
                myImageCodecInfo = GetEncoderInfo("image/jpeg");
                myEncoder = Encoder.Quality;
                myEncoderParameters = new EncoderParameters(1);
                myEncoderParameter = new EncoderParameter(myEncoder, 90L);
                myEncoderParameters.Param[0] = myEncoderParameter;
                i.Save(newFile.FullName, myImageCodecInfo, myEncoderParameters);

            }
            else if (saveType == IsImageExtension.ImageType.PNG)
            {
                i.Save(newFile.FullName, ImageFormat.Png);
            }
            else
                l.Error("Unsupported file type");// never get here 
        }

        //
        // GetEncoderInfo
        //
        // Get an encode for the type of file being created
        //
        private static ImageCodecInfo GetEncoderInfo(String mimeType)
        {
            int j;
            ImageCodecInfo[] encoders;
            encoders = ImageCodecInfo.GetImageEncoders();
            for (j = 0; j < encoders.Length; ++j)
            {
                if (encoders[j].MimeType == mimeType)
                    return encoders[j];
            }
            return null;
        }
    }

}
