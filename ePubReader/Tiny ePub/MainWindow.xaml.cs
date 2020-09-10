using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using eBdb.EpubReader;

namespace Tiny_ePub
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Epub _epub = null;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void MenuFileOpen_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Microsoft.Win32.OpenFileDialog ofd = new Microsoft.Win32.OpenFileDialog();
                ofd.DefaultExt = "epub";
                ofd.Filter = "EPub (*.epub)|*.epub|All Files (*.*)|*.*";
                Nullable<bool> results = ofd.ShowDialog();

                if (results == true)
                {
                    //instantiate epub
                    _epub = new Epub(ofd.FileName);

                    //retrieve document
                    BookDocBrowser.NavigateToString(_epub.GetContentAsHtml());

                    //build info page
                    BuildInfoPage(_epub);

                    //build table of contents
                    foreach (var i in _epub.TOC)
                    {
                        foreach (var u in i.Children)
                        {
                            Console.WriteLine(u.Title);
                        }
                    }

                    BookDocBrowser.Visibility = System.Windows.Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void BuildInfoPage(Epub pub)
        {
            try
            {
                var reader = new StreamReader("BookInfoTemplate.txt");
                var template = reader.ReadToEnd();
                var title = string.Empty;
                var authors = string.Empty;
                var publisher = string.Empty;
                var about = string.Empty;
                
                //get title

                if ((pub.Title.Count > 0) && (!string.IsNullOrEmpty(pub.Title[0])))
                {
                    title = pub.Title[0];
                }
                else
                {
                    title = "<i>No title provided</i>";
                }
                
                //get authors
                if (pub.Creator.Count > 0)
                {
                    foreach (var a in pub.Creator)
                    {
                        authors = a + ", ";
                    }

                    //remove last ", "
                    if (!string.IsNullOrEmpty(authors))
                    {
                        authors = authors.Substring(0, authors.Length - 3);
                    }
                }
                else
                {
                    authors = "<i>No authors listed</i>";
                }

                //get publisher
                if ((pub.Publisher.Count > 0) && (!string.IsNullOrEmpty(pub.Publisher[0])))
                {
                    publisher = pub.Publisher[0];
                }
                else
                {
                    publisher = "<i>No publisher listed</i>";
                }

                //get about
                if ((pub.Description.Count > 0) && (!string.IsNullOrEmpty(pub.Description[0].ToString())))
                {
                    about = pub.Description[0];
                    if (!about.Contains("<p>"))
                    {
                        about = "<p>About: <br />" + about + "</p>";
                    }
                    else
                    {
                        about = about.Insert(about.IndexOf("<p>") + 3, "About: <br />");
                    }
                    about = about.Replace("\\u", " ");
                }
                else
                {
                    about = "<i>No description provided</i>";
                }

                //update template
                template = template.Replace("{title}", title);
                template = template.Replace("{authors}", authors);
                template = template.Replace("{publisher}", publisher);
                template = template.Replace("{about}", about);

                InfoDocBrowser.NavigateToString(template);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void MenuFileExit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MenuViewBoookInfo_Click(object sender, RoutedEventArgs e)
        {
            BookDocBrowser.Visibility = System.Windows.Visibility.Collapsed;
            InfoDocBrowser.Visibility = System.Windows.Visibility.Visible;
        }

        private void MenuViewContent_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("not implemented");
        }

        private void MenuViewBook_Click(object sender, RoutedEventArgs e)
        {
            InfoDocBrowser.Visibility = System.Windows.Visibility.Collapsed;
            BookDocBrowser.Visibility = System.Windows.Visibility.Visible;
        }
    }
}
