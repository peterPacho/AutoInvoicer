using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

/*
 * Todo:
 * - save invoice only when required, fix detecing date change and test detecting service data change
 * - backup - don't backup every time data is written to the database
 * - separate form/window for displaying vin and next oil change due
 * 
*/
namespace AutoInvoicer
{
    public partial class MainWindow : Window
    {
        //load all data from the database, public static so they are all accessible outside this window
        private ObservableCollection<Invoice> InvoiceList = new ObservableCollection<Invoice>();
        private ObservableCollection<Customers> CustomerList = new ObservableCollection<Customers>();
        private bool preventSettingEdit = true;
        private bool searchEnabled = false; //sets true if used the search button, so if invoice is edited and comes back to main screen it will search again instead of listing all the invoices, sets false by refresh button

        public MainWindow()
        {
            SplashScreen splash = new SplashScreen();
            splash.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            splash.Show();

            if (Properties.Settings.Default.SettingsUpgradeRequired)
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.SettingsUpgradeRequired = false;
                Properties.Settings.Default.Save();
            }

            InitializeComponent();

            //load all settings
            checkboxFullScreenInvoiceEditor.IsChecked = Properties.Settings.Default.InvoiceEditorMaximized;
            comboSortingMethod.SelectedIndex = Properties.Settings.Default.InvoiceListSort;
            checkboxExitAfterPDF.IsChecked = Properties.Settings.Default.CloseEditorOnPDF;
            textboxShopInfo.Text = Properties.Settings.Default.ShopInfo;
            labelCustomBackupLocation.Content = Properties.Settings.Default.CustomBackupLocation;
            checkboxFullScreenMain.IsChecked = Properties.Settings.Default.MainWindowMaximized;
            checkboxFilterVehicleWords.IsChecked = Properties.Settings.Default.FilterVehicleField;
            checkboxSearchNewPerfectMatch.IsChecked = Properties.Settings.Default.NewSearchMustMatchAll;
            checkboxSearchServices.IsChecked = Properties.Settings.Default.NewSearchServices;
            checkboxCopyNotes.IsChecked = Properties.Settings.Default.DuplicateInvoiceCopyNotes;
            checkboxDeletePDFs.IsChecked = Properties.Settings.Default.DeletePdfOnClose;
            checkboxDontPrintID.IsChecked = Properties.Settings.Default.DontPrintInvoiceID;
            checkboxSearchMemory.IsChecked = Properties.Settings.Default.RememberSearch;

            //reset the delete button
            Properties.Settings.Default.AllowDeleteInvoice = false;

            if (Properties.Settings.Default.MainWindowMaximized)
                this.WindowState = WindowState.Maximized;

            if (Properties.Settings.Default.RememberSearch)
                textBoxSearch.Text = Properties.Settings.Default.SearchBox;

            LoadData();

            preventSettingEdit = false;

            labelSaveInfo.Visibility = Visibility.Hidden;
            splash.Close();
        }

        /*  
         * Loads data from the database file
        */
        public void refreshFromDatabase()
        {
            bool loadingFailed = false;

            if(!File.Exists("Invoices.db"))
            {
                var result = MessageBox.Show("Database file \"Invoices.db\" doesn't exist. \nDo you want to create new empty database file?", "No database file found.", MessageBoxButton.YesNo);

                if (result == MessageBoxResult.Yes)
                {
                    SqliteDataAccess.createNewFile();
                    refreshFromDatabase();
                    return;
                }
            }
            else
            {
                CustomerList = new ObservableCollection<Customers>(SqliteDataAccess.LoadCustomers(ref loadingFailed));

                if (!loadingFailed)
                    InvoiceList = new ObservableCollection<Invoice>(SqliteDataAccess.LoadInvoices(ref loadingFailed));

                if (loadingFailed)
                {
                    MessageBox.Show("Failed to load data from the database!");
                    return;
                }
            }

            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            this.Title = $"AutoInvoicer v{version.Major}.{version.Minor}.{version.Build}";
            this.Title += " - " + Convert.ToSingle(InvoiceList.Count) + " invoices";
        }

        //used for textbox search
        public void refreshFromDatabase(string textBoxContent)
        {
            bool failedLoading = false;
            InvoiceList = new ObservableCollection<Invoice>(SqliteDataAccess.LoadInvoices(ref failedLoading, textBoxContent));

            if (failedLoading)
            {
                MessageBox.Show("Failed loading data from database (in refreshFromDatabase search function)");
            }

            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            this.Title = $"AutoInvoicer v{version.Major}.{version.Minor}.{version.Build}";
            this.Title += " - " + Convert.ToSingle(InvoiceList.Count) + " invoices";
        }

        public static void SortDataGrid(DataGrid dataGrid, int columnIndex = 0, ListSortDirection sortDirection = ListSortDirection.Descending)
        {
            var column = dataGrid.Columns[columnIndex]; //this column number comes from the user settinig
            var columnID = dataGrid.Columns[0];         //this column number is always 0, so sort by ID

            // Clear current sort descriptions
            dataGrid.Items.SortDescriptions.Clear();

            // Add the new sort description
            dataGrid.Items.SortDescriptions.Add(new SortDescription(column.SortMemberPath, sortDirection));

            //if sorting by MatchesFound, add second crit as sort by date
            if (columnIndex == 4)
                dataGrid.Items.SortDescriptions.Add(new SortDescription(dataGrid.Columns[3].SortMemberPath, ListSortDirection.Descending));

            //also sort by ID, but only if not already sorting by id duh!
            if (columnIndex != 0)
                dataGrid.Items.SortDescriptions.Add(new SortDescription(columnID.SortMemberPath, ListSortDirection.Descending));    //this acts like secondary criterium for sorting, so sort by ID

            // Apply sort
            foreach (var col in dataGrid.Columns)
            {
                col.SortDirection = null;
            }

            column.SortDirection = sortDirection;

            // Refresh items to display sort
            dataGrid.Items.Refresh();
        }

        //used to refresh and update data from the database
        private void LoadData()
        {
            refreshFromDatabase();

            //build custom data list from dbCustomers and dbInvoices, and then put that data to the datagrid
            dataGridInvoices.ItemsSource = null;
            dataGridInvoices.ItemsSource = InvoiceList;
            dataGridInvoices.AutoGenerateColumns = false;

            DataGridTextColumn textColumn = new DataGridTextColumn();
            textColumn.Header = "Invoice ID";
            textColumn.Binding = new Binding("id");
            textColumn.Width = DataGridLength.SizeToHeader;
            dataGridInvoices.Columns.Add(textColumn);

            textColumn = new DataGridTextColumn();
            textColumn.Header = "Vehicle";
            textColumn.Binding = new Binding("vehicleInfo");
            dataGridInvoices.Columns.Add(textColumn);

            textColumn = new DataGridTextColumn();
            textColumn.Header = "Customer";
            textColumn.Binding = new Binding("customerSingleLine");
            dataGridInvoices.Columns.Add(textColumn);

            textColumn = new DataGridTextColumn();
            textColumn.Header = "Invoice date";
            textColumn.Binding = new Binding("invoiceDateFormatted");
            textColumn.Width = 125;
            dataGridInvoices.Columns.Add(textColumn);

            textColumn = new DataGridTextColumn();
            textColumn.Header = "Matches found";
            textColumn.Binding = new Binding("matchesFound");
            textColumn.Width = 130;
            textColumn.Visibility = Visibility.Collapsed;
            dataGridInvoices.Columns.Add(textColumn);

            switch (comboSortingMethod.SelectedIndex)
            {
                case 0:
                    SortDataGrid(dataGridInvoices, 0, ListSortDirection.Ascending);
                    break;

                case 1:
                    SortDataGrid(dataGridInvoices, 0, ListSortDirection.Descending);
                    break;

                case 2:
                    SortDataGrid(dataGridInvoices, 3, ListSortDirection.Ascending);
                    break;

                case 3:
                    SortDataGrid(dataGridInvoices, 3, ListSortDirection.Descending);
                    break;

                default:
                    SortDataGrid(dataGridInvoices, 0);
                    break;
            }

            //fills customer dataGrid skipping the ID field
            dataGridCustomers.ItemsSource = CustomerList;
            dataGridCustomers.AutoGenerateColumns = false;

            textColumn = new DataGridTextColumn();
            textColumn.Header = "Customers list";
            textColumn.Binding = new Binding("customerData");

            dataGridCustomers.Columns.Add(textColumn);

            SortDataGrid(dataGridCustomers, 0, ListSortDirection.Ascending);
        }

        private void dataGridInvoices_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            editCurrentlySelectedInvoice();
        }

        //launch invoiceEditor with editing mode
        private void editCurrentlySelectedInvoice()
        {
            if (dataGridInvoices.SelectedItem == null)
                return;

            //take note which invoice is currently selected, so we can select it back when coming back to main window
            var selec = dataGridInvoices.SelectedIndex;

            InvoiceEditor newInvoiceEditorWindow = new InvoiceEditor(((Invoice)dataGridInvoices.SelectedItem).id);

            this.Visibility = Visibility.Collapsed;

            //this form return true if data was saved at some point
            //(so the refresh in main window is required)
            //flipping the logic - invoice editor returns true only when refresh is not required
            bool? refreshRequired = newInvoiceEditorWindow.ShowDialog();
            if (refreshRequired == null) refreshRequired = true;
            if (refreshRequired == false) refreshRequired = true;

            if (refreshRequired == true)
            {
                //if user searched for data, display only previous search results
                if (searchEnabled)
                {
                    buttonSearchOneTextbox_Click();
                }

                else
                {
                    //refresh all data
                    Button_RefreshData(null, null);
                }

                //restore the previous selected invoice
                dataGridInvoices.SelectedIndex = selec;
            }

            this.Visibility = Visibility.Visible;
        }

        private void button_AddNewCustomer(object sender, RoutedEventArgs e)
        {
            CustomerEditor editWindow = new CustomerEditor();
            editWindow.ShowDialog();
            Button_RefreshData(null, null);
        }

        private void dataGridCustomers_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dataGridCustomers.SelectedItem == null)
                return;

            CustomerEditor editWindow = new CustomerEditor((Customers)dataGridCustomers.SelectedItem);
            editWindow.ShowDialog();

            //refresh all data
            Button_RefreshData(null, null);
        }

        private void Button_NewInvoice(object sender, RoutedEventArgs e)
        {
            tabInvoices.IsSelected = true;
            InvoiceEditor newInvoiceWindow = new InvoiceEditor(-1);
            this.Visibility = Visibility.Collapsed;

            //again - flipped logic - invoice editor returns true only if refresh not required
            bool? refreshRequired = newInvoiceWindow.ShowDialog();
            if (refreshRequired == null) refreshRequired = true;
            if (refreshRequired == false) refreshRequired = true;

            if(refreshRequired == true)
                Button_RefreshData(null, null);

            this.Visibility = Visibility.Visible;
        }

        private void Button_RefreshData(object sender, RoutedEventArgs e)
        {
            refreshFromDatabase();
            dataGridInvoices.ItemsSource = null;
            dataGridInvoices.ItemsSource = InvoiceList;

            dataGridCustomers.ItemsSource = null;
            dataGridCustomers.ItemsSource = CustomerList;

            searchEnabled = false;
            ButtonRefresh.Content = "Refresh";
            dataGridInvoices.Columns[4].Visibility = Visibility.Collapsed;

            switch (comboSortingMethod.SelectedIndex)
            {
                case 0:
                    SortDataGrid(dataGridInvoices, 0, ListSortDirection.Ascending);
                    break;

                case 1:
                    SortDataGrid(dataGridInvoices, 0, ListSortDirection.Descending);
                    break;

                case 2:
                    SortDataGrid(dataGridInvoices, 3, ListSortDirection.Ascending);
                    break;

                case 3:
                    SortDataGrid(dataGridInvoices, 3, ListSortDirection.Descending);
                    break;

                default:
                    SortDataGrid(dataGridInvoices, 0);
                    break;
            }

            SortDataGrid(dataGridCustomers, 0, ListSortDirection.Ascending);
        }

        private void checkboxFullScreenInvoiceEditorChanged(object sender, RoutedEventArgs e)
        {
            if (preventSettingEdit) return;

            Properties.Settings.Default.InvoiceEditorMaximized = checkboxFullScreenInvoiceEditor.IsChecked.Value;

            Properties.Settings.Default.Save();
            labelSaveInfo.Visibility = Visibility.Visible;
        }


        private void comboSortingMethod_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (preventSettingEdit) return;

            Properties.Settings.Default.InvoiceListSort = comboSortingMethod.SelectedIndex;
            Properties.Settings.Default.Save();
            labelSaveInfo.Visibility = Visibility.Visible;
        }

        private void checkboxFullScreenMain_Checked(object sender, RoutedEventArgs e)
        {
            if (preventSettingEdit) return;

            Properties.Settings.Default.MainWindowMaximized = checkboxFullScreenMain.IsChecked.Value;

            Properties.Settings.Default.Save();
            labelSaveInfo.Visibility = Visibility.Visible;
        }

        private void checkboxExitAfterPDF_Checked(object sender, RoutedEventArgs e)
        {
            if (preventSettingEdit) return;

            Properties.Settings.Default.CloseEditorOnPDF = checkboxExitAfterPDF.IsChecked.Value;

            Properties.Settings.Default.Save();
            labelSaveInfo.Visibility = Visibility.Visible;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (preventSettingEdit) return;
            Properties.Settings.Default.ShopInfo = textboxShopInfo.Text;
            Properties.Settings.Default.Save();
        }

        private void ButtonResetSettings_click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure that you want to reset all settings to default values?\nProgram must be manually restarted and will close if you select 'YES'.", "Reset settings to default", MessageBoxButton.YesNo);

            if (result == MessageBoxResult.Yes)
            {
                Properties.Settings.Default.Reset();
                Properties.Settings.Default.Save();
                this.Close();
            }
        }

        private void labelSaveInfo_MouseDown(object sender, MouseButtonEventArgs e)
        {
            labelSaveInfo.Visibility = Visibility.Hidden;
        }

        private void textboxShopInfo_TextChanged(object sender, TextChangedEventArgs e)
        {
            labelSaveInfo.Visibility = Visibility.Visible;
        }


        private void dataGridContext_Edit(object sender, RoutedEventArgs e)
        {
            editCurrentlySelectedInvoice();
        }

        private void dataGridContext_EditCustomerRecord(object sender, RoutedEventArgs e)
        {
            dataGridCustomers_MouseDoubleClick(null, null);
        }

        private void dataGridContext_PDF(object sender, RoutedEventArgs e)
        {
            if (dataGridInvoices.SelectedItem == null)
                return;

            //find all info and services of the requested invoice
            bool failedLoading = false;
            Invoice invoiceToPrint = SqliteDataAccess.LoadSingleInvoice(ref failedLoading, ((Invoice)dataGridInvoices.SelectedItem).id);

            if (failedLoading)
            {
                MessageBox.Show("Failed to load invoice to generate PDF!");
                return;
            }

            PdfGenerate.generateInvoice(invoiceToPrint);
        }

        private void dataGridContext_FindVin(object sender, RoutedEventArgs e)
        {
            if (dataGridInvoices.SelectedItem == null)
                return;

            textBoxSearch.Text = ((Invoice)dataGridInvoices.SelectedItem).vin;
            buttonSearchOneTextbox_Click();
        }

        private void dataGridContext_FindCustomer(object sender, RoutedEventArgs e)
        {
            if (dataGridInvoices.SelectedItem == null)
                return;

            string customerFirstLine = ((Invoice)dataGridInvoices.SelectedItem).customer;

            if (customerFirstLine.Contains("\n"))
            {
                try
                {
                    customerFirstLine = customerFirstLine.Substring(0, customerFirstLine.IndexOf("\n") - 1);
                }

                catch
                {

                }
            }

            if (customerFirstLine.Length > 12)
                customerFirstLine = customerFirstLine.Substring(0, 10);

            textBoxSearch.Text = customerFirstLine;
            buttonSearchOneTextbox_Click();
        }

        private void dataGridInvoices_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                editCurrentlySelectedInvoice();
            }
        }


        private void checkboxSearchMemory_Checked(object sender, RoutedEventArgs e)
        {
            if (preventSettingEdit) return;

            Properties.Settings.Default.RememberSearch = checkboxSearchMemory.IsChecked.Value;

            Properties.Settings.Default.Save();
            labelSaveInfo.Visibility = Visibility.Visible;
        }

        private void checkboxFilterVehicleWords_Checked(object sender, RoutedEventArgs e)
        {
            if (preventSettingEdit) return;

            Properties.Settings.Default.FilterVehicleField = checkboxFilterVehicleWords.IsChecked.Value;

            Properties.Settings.Default.Save();
            labelSaveInfo.Visibility = Visibility.Visible;
        }

        private void Button_Backup_Location_Click(object sender, RoutedEventArgs e)
        {
            if (preventSettingEdit) return;
            //create dialog for picking file location
            var dialog = new Microsoft.Win32.SaveFileDialog();
            dialog.FileName = "AutoInvoicer Database Backup";
            dialog.DefaultExt = ".zip";
            dialog.Filter = "Zip archive (.zip)|*.zip";

            bool? result = dialog.ShowDialog();

            if (result == true)
            {
                Properties.Settings.Default.CustomBackupLocation = dialog.FileName;
                Properties.Settings.Default.CustomBackupLastUpdated = "0";
                Properties.Settings.Default.Save();
            }

            labelCustomBackupLocation.Content = dialog.FileName;

            labelSaveInfo.Visibility = Visibility.Visible;
        }

        private void Button_Backup_Location_Clear(object sender, RoutedEventArgs e)
        {
            if (preventSettingEdit) return;
            Properties.Settings.Default.CustomBackupLocation = "";
            Properties.Settings.Default.Save();
            labelCustomBackupLocation.Content = "";
            Properties.Settings.Default.CustomBackupLastUpdated = "0";

            labelSaveInfo.Visibility = Visibility.Visible;
        }



        private void buttonSearchOneTextbox_Click(object sender, RoutedEventArgs e)
        {
            buttonSearchOneTextbox_Click();
        }
        private void buttonSearchOneTextbox_Click()
        {
            searchEnabled = true;
            ButtonRefresh.Content = "Refresh and clear search results";
            refreshFromDatabase(textBoxSearch.Text);
            dataGridInvoices.ItemsSource = null;
            dataGridInvoices.ItemsSource = InvoiceList;
            SortDataGrid(dataGridInvoices, 4, ListSortDirection.Descending);

            if (!Properties.Settings.Default.NewSearchMustMatchAll)
                dataGridInvoices.Columns[4].Visibility = Visibility.Visible;
            else
                dataGridInvoices.Columns[4].Visibility = Visibility.Hidden;

            Properties.Settings.Default.SearchBox = textBoxSearch.Text;
            Properties.Settings.Default.Save();
        }

        private void dockPanelNewSearch_PreviewKeyDown(object sender, KeyEventArgs e)
        {

            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                buttonSearchOneTextbox_Click();
            }

            else if (e.Key == Key.Escape)
            {
                e.Handled = true;

                //if searching for something, come back to main screen
                if (searchEnabled)
                    Button_RefreshData(null, null);
                //if search not enabled, clear all fields
                else
                    textBoxSearch.Text = "";
            }

        }

        private void checkboxSearchNewPerfectMatch_Checked(object sender, RoutedEventArgs e)
        {
            if (preventSettingEdit) return;

            Properties.Settings.Default.NewSearchMustMatchAll = checkboxSearchNewPerfectMatch.IsChecked.Value;
            Properties.Settings.Default.Save();
            labelSaveInfo.Visibility = Visibility.Visible;
        }

        private void checkboxSearchServices_Checked(object sender, RoutedEventArgs e)
        {
            if (preventSettingEdit) return;

            Properties.Settings.Default.NewSearchServices = checkboxSearchServices.IsChecked.Value;
            Properties.Settings.Default.Save();
            labelSaveInfo.Visibility = Visibility.Visible;
        }

        private void checkboxCopyNotes_Checked(object sender, RoutedEventArgs e)
        {
            if (preventSettingEdit) return;

            Properties.Settings.Default.DuplicateInvoiceCopyNotes = checkboxCopyNotes.IsChecked.Value;
            Properties.Settings.Default.Save();
            labelSaveInfo.Visibility = Visibility.Visible;
        }

        private void checkboxAllowDelete_Checked(object sender, RoutedEventArgs e)
        {
            if (preventSettingEdit) return;

            Properties.Settings.Default.AllowDeleteInvoice = checkboxAllowDelete.IsChecked.Value;
            Properties.Settings.Default.Save();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (Properties.Settings.Default.DeletePdfOnClose)
            {
                if (Directory.Exists(@"./PDFs/"))
                {
                    try
                    {
                        Directory.Delete(@"./PDFs/", true);
                    }

                    catch 
                    {

                    }
                }
            }
        }

        private void checkboxDeletePDFs_Checked(object sender, RoutedEventArgs e)
        {
            if (preventSettingEdit) return;

            Properties.Settings.Default.DeletePdfOnClose = checkboxDeletePDFs.IsChecked.Value;
            Properties.Settings.Default.Save();
        }

        private void checkboxDontPrintID_Checked(object sender, RoutedEventArgs e)
        {
            if (preventSettingEdit) return;

            Properties.Settings.Default.DontPrintInvoiceID = checkboxDontPrintID.IsChecked.Value;
            Properties.Settings.Default.Save();
        }
    }
}
