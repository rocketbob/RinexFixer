using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace RinexFixer
{
    public partial class MainWindow : Window
    {
        string _fileName;
        string _fileExtension;
        string _filePath;
        string _fullFilePathName;
        bool _processingMultipleFiles = false; // used only when processing entire directory

        public MainWindow()
        {
            InitializeComponent();
        }

        private void OpenFolder(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var fbd = new FolderBrowserDialog())
                {
                    DialogResult result = fbd.ShowDialog();

                    if (!string.IsNullOrWhiteSpace(fbd.SelectedPath))
                    {
                        var files = Directory.GetFiles(fbd.SelectedPath);
                        var bldr = new StringBuilder();
                        foreach(var f in files)
                        {
                            bldr.Append(f + "\n");
                        }
                        txtEditor.Text = bldr.ToString();
                    }
                    _processingMultipleFiles = true;
                }
            }
            catch(Exception ex)
            {
                MessageBoxResult msgBox = System.Windows.MessageBox.Show(ex.ToString(), "RinexFixer Error");
            }
        }

        private void OpenFile(object sender, RoutedEventArgs e)
        {
            try
            {
                Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
                openFileDialog.Filter = "Rinex Files (*.**o)|*.*o|All files (*.*)|*.*";
                if (openFileDialog.ShowDialog() == true)
                {
                    _fileName = System.IO.Path.GetFileNameWithoutExtension(openFileDialog.FileName);
                    _fileExtension = System.IO.Path.GetExtension(openFileDialog.FileName);
                    _filePath = System.IO.Path.GetDirectoryName(openFileDialog.FileName);
                    _fullFilePathName = openFileDialog.FileName;
                    _processingMultipleFiles = false;

                    txtEditor.Text = File.ReadAllText(openFileDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                MessageBoxResult msgBox = System.Windows.MessageBox.Show(ex.ToString(), "RinexFixer Error");
            }
        }

        private void Fix(object sender, RoutedEventArgs e)
        {
            try
            {
                if(_processingMultipleFiles == true) // working on a dir
                {
                    var files = txtEditor.Text.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var f in files)
                    {
                        if (File.Exists(f))
                        {
                            var text = File.ReadAllText(f);
                            fixFile(text, System.IO.Path.GetFileNameWithoutExtension(f),
                                System.IO.Path.GetExtension(f),
                                System.IO.Path.GetDirectoryName(f)
                            );
                        }
                    }
                }
                else // working individual file
                {
                    fixFile(txtEditor.Text, _fileName, _fileExtension, _filePath);
                }
            }
            catch(Exception ex)
            {
                MessageBoxResult msgBox = System.Windows.MessageBox.Show(ex.ToString(), "RinexFixer Error");
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private string padSpace(string value)
        {
            if (value.Length == 1)
                return " " + value;
            else
                return value;
        }

        private string fixFile(string fileText, string fileName, string fileExtension, string filePath)
        {
            var resultText = string.Empty;
            DateTime headerDate = DateTime.Now;
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            var output = new StringBuilder();
            using (StringReader reader = new StringReader(fileText))
            {
                string line;
                bool startParsingBody = false;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Contains("END OF HEADER"))
                        startParsingBody = true; // parse body
                    else // look for dates in header
                    {
                        var possibleDate = line.Substring(2, 16);
                        DateTime test;
                        if (DateTime.TryParseExact(possibleDate, "yyyy     M     d", CultureInfo.InvariantCulture, DateTimeStyles.AllowInnerWhite, out test))
                        {  
                            var newDate = test.AddDays(1024 * 7); // ignore anything over 5 years old in case we pick up a bogus date.  Not perfect but will do.
                            if (newDate >= DateTime.Now.AddYears(-5))
                            {
                                string year = newDate.ToString("yyyy");
                                string mo = padSpace(newDate.Month.ToString());
                                string day = padSpace(newDate.Day.ToString());
                                line = line.Replace(possibleDate, year + "    " + mo + "    " + day);
                                headerDate = newDate;
                            }
                        }
                    }

                    if (startParsingBody)
                    {
                        var possibleDate = line.Substring(1, 8);
                        DateTime test;
                        if (DateTime.TryParseExact(possibleDate, "yy M d", CultureInfo.InvariantCulture, DateTimeStyles.AllowInnerWhite, out test))
                        {
                            var newDate = test.AddDays(1024 * 7); // ignore anything over 5 years old in case we pick up a bogus date.  Not perfect but will do.
                            if (newDate >= headerDate.AddDays(-1) && newDate <= headerDate) // +1 days and no more
                            {
                                string year = newDate.ToString("yy");
                                string mo = padSpace(newDate.Month.ToString());
                                string day = padSpace(newDate.Day.ToString());
                                line = line.Replace(possibleDate, year + " " + mo + " " + day);
                            }

                        }
                    }
                    output.Append(line);
                    output.Append(Environment.NewLine);
                }
            }

            resultText = output.ToString();
            System.IO.File.WriteAllText(filePath + "\\" + fileName + "." + DateTime.Now.ToString("yy") + "o", resultText);
            return resultText;
        }
    }
}
