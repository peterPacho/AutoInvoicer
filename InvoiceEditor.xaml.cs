using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace AutoInvoicer
{
    public partial class InvoiceEditor : Window
    {
        private List<Customers> dbCustomers = new List<Customers>();
        private Invoice invoice = new Invoice();
        private bool saveRequired = false; //if set to true then saving the data required
        private bool stopUpdating = true; //stops updating the elements when data is loaded
        private bool refreshRequired = false; //set to true if data is saved
        private string[,] replacementArray = new string[,]
                {
                    {"/p", "              - parts" },
                    {"/l", "              - labor" },
                    {"/fb", "Front brakes - pads and rotors" },
                    {"/rb", "Rear brakes - pads and rotors" },
                    {"/o", "Oil and oil filter change" },
                    {"/ac", "Recharge A/C System" },
                    {"/r", "              - replacement" },
                    {"/m1", "Mount and balance one tire" },
                    {"/m2", "Mount and balance two tires" },
                    {"/m3", "Mount and balance three tires" },
                    {"/m4", "Mount and balance four tires" },
                };



        //load all customers to populate the combo box
        private void loadCustomerListBox()
        {
            bool failedLoading = false;

            dbCustomers = SqliteDataAccess.LoadCustomers(ref failedLoading);

            foreach (Customers customerName in dbCustomers)
            {
                string customerNameBuffer = customerName.customerData;

                //select only first line
                if (customerNameBuffer.Contains("\n"))
                {
                    customerNameBuffer = customerNameBuffer.Substring(0, customerNameBuffer.IndexOf("\n") - 1);
                }

                //get rid of lenghty names
                if (customerNameBuffer.Length > 40)
                {
                    customerNameBuffer = customerNameBuffer.Substring(0, 40);
                }

                comboBoxCustomerList.Items.Add(customerNameBuffer);
            }
        }


        //having bunch of dummy records makes it easier to edit
        private void fillListWithDummyRecords()
        {
            int numberToAdd = 0;

            if (invoice.services != null)
                numberToAdd = invoice.services.Count;
            else
                invoice.services = new List<Service>();

            for (int i = numberToAdd; i < 12; i++)
                invoice.services.Add(new Service());

            dataGridServices.ItemsSource = null;
            dataGridServices.ItemsSource = invoice.services;
        }


        //used to validate data in data grid (to make sure that cost contains only numbers) and sum up the total cost
        void checkData()
        {
            double serviceSum = 0;

            //go over all entered services
            for (int i = 0; i < invoice.services.Count; i++)
            {
                serviceSum += Convert.ToDouble(invoice.services[i].cost);
                serviceSum = Math.Round(serviceSum, 2);

                //search all service entries for special strings like "/l" and replace them with proper text
                for (int special = 0; special < (replacementArray.Length / 2); special++)
                {
                    if (invoice.services[i].service == replacementArray[special, 0])
                    {
                        invoice.services[i].service = replacementArray[special, 1];
                    }
                }

                //if that entry contains just the / sign, add couple spaces before it
                if (invoice.services[i].service.Contains("/") && invoice.services[i].service.IndexOf("/") == 0)
                {
                    invoice.services[i].service = "              - " + invoice.services[i].service.Substring(1, invoice.services[i].service.Length - 1);
                }

            }

            labelTotal.Content = "Invoice total : $" + Convert.ToString(serviceSum);
        }

        void dataChanged()
        {
            if (stopUpdating) return;

            if (!saveRequired)
            {
                this.Title = this.Title + "*";
            }

            saveRequired = true;
        }

        //gets called when requesting editing existing invoice
        public InvoiceEditor(int id = -1, string defVehVin = "", string defVehInfo = "", string defCustInfo = "", string defVehMileage = "", string defNote = "", List<Service> defServices = null, bool forceReload = false)
        {
            InitializeComponent();

            if (Properties.Settings.Default.InvoiceEditorMaximized)
                this.WindowState = WindowState.Maximized;

            if (Properties.Settings.Default.CloseEditorOnPDF)
            {
                buttonSavePrint.Content = "Generate PDF and close";
                buttonSavePrint_alternative.Header = "Generate PDF but DO NOT close this window";
            }

            if (Properties.Settings.Default.DuplicateInvoiceCopyNotes)
                buttonDuplicateKeepNote.Header = "Duplicate this record but discard all notes";

            //do those things only when loading the existing invoice, otherwise 
            if (id != -1)
            {
                //load requested record data straight from the database
                bool failedLoading = false;
                invoice = SqliteDataAccess.LoadSingleInvoice(ref failedLoading, id);

                if(failedLoading)
                {
                    MessageBox.Show("Failed to load the invoice id " + id.ToString());
                    this.Close();
                }

                //edit button labels to reflect that we are indeed editing
                Title = "Invoice Editor - Editing invoice with ID " + Convert.ToString(invoice.id);
                buttonCancel.Content = "Cancel editing";

                //update all fields in the form
                textBoxVin.Text = invoice.vin;
                textBoxVehInfo.Text = invoice.vehicleInfo;
                textBoxNotes.Text = invoice.notes;

                if (invoice.mileage != null && invoice.mileage != 0)
                    textBoxMileage.Text = Convert.ToString(invoice.mileage);

                textBoxCustomerInfo.Text = invoice.customer;

                if (!Properties.Settings.Default.AllowDeleteInvoice)
                    buttonDelete.Visibility = Visibility.Collapsed;

                string date = Convert.ToString(invoice.invoiceDate);
                if (date.Length == 8) //if in proper date format
                {
                    date = date.Insert(4, "/");
                    date = date.Insert(7, "/");

                    try
                    {
                        dateInvoice.SelectedDate = DateTime.Parse(date);
                    }

                    catch {}
                }
            }

            else
            {
                Title = "Invoice Editor - creating new invoice";
                dateInvoice.SelectedDate = DateTime.Today;
                buttonDelete.Visibility = Visibility.Collapsed;
                buttonDuplicate.Visibility = Visibility.Collapsed;

                //fill info if given
                textBoxVin.Text = defVehVin;
                textBoxVehInfo.Text = defVehInfo;
                textBoxMileage.Text = defVehMileage;
                textBoxCustomerInfo.Text = defCustInfo;
                textBoxNotes.Text = defNote;

                if (defServices != null)
                    invoice.services = defServices;

                invoice.id = -1;
            }

            loadCustomerListBox();

            dataGridServices.ItemsSource = invoice.services;
            dataGridServices.AutoGenerateColumns = false;

            DataGridTextColumn textColumnSmallDesc = new DataGridTextColumn();
            textColumnSmallDesc.Binding = new Binding("service");
            textColumnSmallDesc.Header = "Service";
            dataGridServices.Columns.Add(textColumnSmallDesc);

            DataGridTextColumn textColumnSmallPrice = new DataGridTextColumn();
            textColumnSmallPrice.Binding = new Binding("costGrid");
            textColumnSmallPrice.Binding.StringFormat = "{0}";
            textColumnSmallPrice.Header = "Cost";
            textColumnSmallPrice.Binding.TargetNullValue = "";
            textColumnSmallPrice.Binding.FallbackValue = "";

            textColumnSmallPrice.Width = 100;

            dataGridServices.Columns.Add(textColumnSmallPrice);

            fillListWithDummyRecords();

            checkData();

            dataGridServices.ToolTip = "You can use following shortcuts to fill out service fields: \n";
            for (int ee = 0; ee < (replacementArray.Length / 2); ee++)
            {
                dataGridServices.ToolTip += replacementArray[ee, 0] + " for " + replacementArray[ee, 1] + "\n";
            }

            dataGridServices.ToolTip += "\nSimply type just the " + replacementArray[0, 0] + " and press enter \nand it will be replaced with \n" + replacementArray[0, 1];

            stopUpdating = false;
            saveRequired = forceReload; //in case this window is launched in "duplicate record" mode then force refresh of the main window

            //if used it only to filter, then close immediately
            if (defNote == "32k42yn34ijy4mof543572d564dfre5g3jhg32h4g324")
            {
                saveToDatabase();
                this.Close();
            }
        }


        //this version used when duplicating record but keeping the services
        public InvoiceEditor(Invoice invoice) : this(-1, invoice.vin, invoice.vehicleInfo, invoice.customer, invoice.mileage.ToString(), invoice.notes, invoice.services) { }

        private void comboBoxCustomerList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //if this method is called by itself
            if (comboBoxCustomerList.SelectedIndex < 0)
                return;


            //check if textbox is almost empty, if not then ask if you really want to update it
            if (textBoxCustomerInfo.Text != "" || textBoxCustomerInfo.Text.Length > 5)
            {
                var result = MessageBox.Show("Do you want to update the customer info anyways?\nThat will overwrite all data in customer info field!", "Customer info field not empty.", MessageBoxButton.YesNo);

                if (result == MessageBoxResult.No)
                {
                    comboBoxCustomerList.Text = "";
                    comboBoxCustomerList.SelectedIndex = -1;
                    return;
                }
            }



            textBoxCustomerInfo.Text = dbCustomers[comboBoxCustomerList.SelectedIndex].customerData;
            comboBoxCustomerList.Text = "";
            comboBoxCustomerList.SelectedIndex = -1;
        }

        private void buttonCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = refreshRequired;
            this.Close();
        }



        public int saveToDatabase()
        {
            refreshRequired = true;

            //remove spaces in vehicle info field
            if (Properties.Settings.Default.FilterVehicleField)
            {
                try
                {
                    while (textBoxVehInfo.Text.Contains("  "))
                        textBoxVehInfo.Text = textBoxVehInfo.Text.Replace("  ", " ");
                    //space in front of the text
                    if (textBoxVehInfo.Text.IndexOf(" ") == 0)
                        textBoxVehInfo.Text = textBoxVehInfo.Text.Substring(1, textBoxVehInfo.Text.Length - 1);
                }
                catch{ }
            }

            //remove spaces from the VIN
            textBoxVin.Text = textBoxVin.Text.Replace(" ", "");

            //build invoice object from data in the current window
            invoice.vin = textBoxVin.Text;
            invoice.vehicleInfo = textBoxVehInfo.Text;
            invoice.notes = textBoxNotes.Text;

            textBoxMileage.Text = textBoxMileage.Text.Replace(",", "");
            textBoxMileage.Text = textBoxMileage.Text.Replace(".", "");

            try
            {
                invoice.mileage = Convert.ToInt32(textBoxMileage.Text);
            }
            catch
            {
                invoice.mileage = 0;
            }

            invoice.customer = textBoxCustomerInfo.Text;

            //convert date from timedate format to string and finally to int
            if (dateInvoice.ToString() != "")
            {
                DateTime invoiceDate = Convert.ToDateTime(dateInvoice.SelectedDate);

                string dateInStrFormat = Convert.ToString(invoiceDate.Year); //yyyymmdd
                if (invoiceDate.Month < 10) dateInStrFormat += "0";
                dateInStrFormat += Convert.ToString(invoiceDate.Month);
                if (invoiceDate.Day < 10) dateInStrFormat += "0";
                dateInStrFormat += Convert.ToString(invoiceDate.Day);

                invoice.invoiceDate = Convert.ToInt32(dateInStrFormat);
            }
            else
            {
                invoice.invoiceDate = null;
            }

            //assume that foundServices contains empty records, delete last empty records
            int positionOfLastValidRecord = 0;

            for (int i = 0; i < invoice.services.Count; i++)
            {
                if (invoice.services[i].service.Length > 0 || invoice.services[i].cost.HasValue)
                    positionOfLastValidRecord = i;
            }


            for (int i = positionOfLastValidRecord + 1; invoice.services.Count - 1 > positionOfLastValidRecord; i++)
            {
                invoice.services.RemoveAt(positionOfLastValidRecord + 1);
            }


            //send to the database
            return SqliteDataAccess.SaveInvoice(invoice);
        }


        private void buttonSave_Click(object sender, RoutedEventArgs e)
        {
            if (saveToDatabase() != -1)
            {
                this.Close();
            }
            else
                MessageBox.Show("Saving to the database failed.");
        }

        private void buttonDelete_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure that you want to delete this invoice?\n\nThis operation cannot be undone!", "Deleting invoice record", MessageBoxButton.YesNo);

            if (result == MessageBoxResult.Yes)
            {
                SqliteDataAccess.DeleteInvoice(invoice.id);
                this.Close();
            }
        }

        private void textBoxMileage_TextChanged(object sender, TextChangedEventArgs e)
        {
            dataChanged();
            
            int boxValue = 0;

            if (!int.TryParse(textBoxMileage.Text, out boxValue))
            {
                labelMileageWarning.Visibility = Visibility.Visible;
                textBoxMileage.ToolTip = null;
            }
            else
            {
                labelMileageWarning.Visibility = Visibility.Hidden;

                //round next due mileage to nearest thousand
                int nextChange = boxValue - boxValue % 1000 + 6000;

                if (boxValue % 1000 >= 400) nextChange += 1000;

                textBoxMileage.ToolTip = "Next oil change in ~6k miles due at " + Convert.ToString(nextChange / 1000) + "k\n(that puts next oil change in " + Convert.ToString(nextChange - boxValue) + " miles)";
            }
        }

        private void buttonSavePrint_Click(object sender, RoutedEventArgs e)
        {
            bool closeEditorWhenDone = Properties.Settings.Default.CloseEditorOnPDF;
            refreshRequired = true;

            //if this event called by context menu option, do separate function of what main button is saying
            if (((FrameworkElement)sender).Name == "buttonSavePrint_alternative")
                closeEditorWhenDone = !closeEditorWhenDone;

            int returnedInvoiceID = saveToDatabase();

            if (returnedInvoiceID != -1)
            {
                PdfGenerate.generateInvoice(invoice);
                invoice.id = returnedInvoiceID;

                if (closeEditorWhenDone)
                {
                    this.Close();
                }
            }
            else
            {
                MessageBox.Show("PDF generation aborted.\nCouldn't save the invoice to the database.");
            }


            fillListWithDummyRecords();
        }

        private void textBoxVehInfo_TextChanged(object sender, TextChangedEventArgs e)
        {
            dataChanged();

            if (Properties.Settings.Default.FilterVehicleField)
            {
                //remove special words from the textbox

                string[] wordFilter = new string[]
                {
                "Truck",
                "-Datsun",
                "Econoline",
                "Benz"
                };

                for (int filter = 0; filter < wordFilter.Length; filter++)
                {
                    if (textBoxVehInfo.Text.Contains(wordFilter[filter]))
                    {
                        try
                        {
                            textBoxVehInfo.Text =
                                //first part, from 0 to whatever word was found
                                textBoxVehInfo.Text.Substring(0, textBoxVehInfo.Text.IndexOf(wordFilter[filter])) +

                                //second part, from word that was found to the end
                                textBoxVehInfo.Text.Substring(textBoxVehInfo.Text.IndexOf(wordFilter[filter]) + wordFilter[filter].Length);
                        }

                        catch
                        {

                        }
                    }
                }


                //try to get rid of page breaks
                try
                {
                    int cursorPosition = textBoxVehInfo.SelectionStart;
                    bool restoreCursorPosition = false;




                    //restore cursor position only when special characters are found
                    if (textBoxVehInfo.Text.Contains(Environment.NewLine) || textBoxVehInfo.Text.IndexOf(" ") == 0)
                    {
                        restoreCursorPosition = true;
                    }

                    textBoxVehInfo.Text = textBoxVehInfo.Text.Replace(Environment.NewLine, " ");


                    //this part is now done only right before saving
                    /*
                    textBoxVehInfo.Text = textBoxVehInfo.Text.Replace("  ", " ");
                    //space in front of the text
                    if (textBoxVehInfo.Text.IndexOf(" ") == 0)
                        textBoxVehInfo.Text = textBoxVehInfo.Text.Substring(1, textBoxVehInfo.Text.Length - 1);
                    */


                    //sanity checks
                    if (cursorPosition > textBoxVehInfo.Text.Length)
                        cursorPosition = textBoxVehInfo.Text.Length;
                    if (cursorPosition < 0)
                        cursorPosition = 0;


                    if (restoreCursorPosition)
                        textBoxVehInfo.Select(cursorPosition, 0);
                }

                catch
                {

                }


                //if there is a sequence of 17 characters no space in between, then assume it's a vin and move it to vin field if vin field empty

                if (textBoxVin.Text == "")
                {
                    string[] splitVinInfo = textBoxVehInfo.Text.Split();

                    for (int i = 0; i < splitVinInfo.Length; i++)
                    {
                        //check if it might be vin number
                        if (splitVinInfo[i].Length == 17 && Regex.IsMatch(splitVinInfo[i], @"^[a-zA-Z0-9]+$"))
                        {

                            textBoxVin.Text = splitVinInfo[i];

                            textBoxVehInfo.Text = textBoxVehInfo.Text.Substring(0, textBoxVehInfo.Text.IndexOf(splitVinInfo[i]));
                        }
                    }
                }
            }



        }

        private void buttonDuplicate_Click(object sender, RoutedEventArgs e)
        {
            // Get the element that handled the event.
            FrameworkElement fe = (FrameworkElement)sender;

            saveToDatabase();

            InvoiceEditor newInvoiceEditorWindow;


            if (fe.Name == "buttonDuplicateKeepNote")
            {
                string notes = "";

                if (!Properties.Settings.Default.DuplicateInvoiceCopyNotes)
                    notes = textBoxNotes.Text;

                newInvoiceEditorWindow = new InvoiceEditor(-1, textBoxVin.Text, textBoxVehInfo.Text, textBoxCustomerInfo.Text, textBoxMileage.Text, notes);

            }
            else if (fe.Name == "buttonDuplicateKeepNoteService")
            {
                newInvoiceEditorWindow = new InvoiceEditor(invoice);
            }
            else if (fe.Name == "buttonDuplicateKeepService")
            {
                newInvoiceEditorWindow = new InvoiceEditor(-1, textBoxVin.Text, textBoxVehInfo.Text, textBoxCustomerInfo.Text, textBoxMileage.Text, "", invoice.services);
            }
            else
            {
                string notes = "";

                if (Properties.Settings.Default.DuplicateInvoiceCopyNotes)
                    notes = textBoxNotes.Text;

                newInvoiceEditorWindow = new InvoiceEditor(-1, textBoxVin.Text, textBoxVehInfo.Text, textBoxCustomerInfo.Text, textBoxMileage.Text, notes);

            }

            this.Visibility = Visibility.Hidden;

            newInvoiceEditorWindow.ShowDialog();

            this.Close();
        }

        private void textBoxCustomerInfo_TextChanged(object sender, TextChangedEventArgs e)
        {
            dataChanged();

            if (textBoxCustomerInfo.Text.IndexOf(" ") == 0)
                textBoxCustomerInfo.Text = textBoxCustomerInfo.Text.Substring(1, textBoxCustomerInfo.Text.Length - 1);
        }

        private void textBoxVin_TextChanged(object sender, TextChangedEventArgs e)
        {
            dataChanged();

            textBoxVin.Text = textBoxVin.Text.ToUpper();

            if (textBoxVin.Text.Length != 0 && textBoxVin.Text.Length != 17)
                textBoxVin.ToolTip = "Proper VIN has 17 characters. This one has " + Convert.ToString(textBoxVin.Text.Length) + " characters.";
            else
            {
                try
                {
                    textBoxVin.ToolTip = textBoxVin.Text.Substring(0, 5) + " " + textBoxVin.Text.Substring(5, 4) + " " + textBoxVin.Text.Substring(9, 4) + " " + textBoxVin.Text.Substring(13, 4);
                }
                catch
                { }
            }
        }

        private void dataGridContext_Button(object sender, RoutedEventArgs e)
        {
            //used to figure out which contect menu button was called
            FrameworkElement fe = (FrameworkElement)sender;
            int indexOfSelecedElement;

            //used to figure out which row is selected when context butto was pressed
            if (dataGridServices.SelectedItem == null)
                return;

            Service currentSelected;

            try
            {
                currentSelected = (Service)dataGridServices.SelectedItem;
            }

            catch
            {
                //if previous failed, assume that user clicked on last (non existing) element, so add the empty element, select this element, and continue
                invoice.services.Add(new Service());
                currentSelected = invoice.services[invoice.services.Count - 1];

                //but if requested adding new row, continuing will cause two new rows to be created, so return instead
                if (fe.Name == "dataGridContext_InsertBelow" || fe.Name == "dataGridContext_InsertAbove")
                    return;
            }


            indexOfSelecedElement = invoice.services.IndexOf(currentSelected);

            //MessageBox.Show(Convert.ToString(indexOfSelecedElement));



            //figure out which button was called
            if (fe.Name == "dataGridContext_InsertAbove")
            {
                invoice.services.Insert(indexOfSelecedElement, new Service());
            }

            else if (fe.Name == "dataGridContext_InsertBelow")
            {
                invoice.services.Insert(indexOfSelecedElement + 1, new Service());
            }

            else if (fe.Name == "dataGridContext_RemoveRow")
            {
                invoice.services.Remove(currentSelected);
            }

            else if (fe.Name == "dataGridContext_InsertLabor")
            {
                invoice.services[indexOfSelecedElement].service = "              - labor";
            }

            else if (fe.Name == "dataGridContext_InsertParts")
            {
                invoice.services[indexOfSelecedElement].service = "              - parts";
            }

            dataGridServices.ItemsSource = null;
            dataGridServices.ItemsSource = invoice.services;
        }

        private void labelTotal_MouseUp(object sender, MouseButtonEventArgs e)
        {
            checkData();
        }

        private void dataGridServices_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            dataChanged();
            checkData();
        }

        private void dataGridServices_CurrentCellChanged(object sender, EventArgs e)
        {
            dataChanged();
            checkData();
        }

        //if enter key pressed, do custom updates
        private void dataGridServices_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            /*
            var uiElement = e.OriginalSource as UIElement;
            if (e.Key == Key.Enter && uiElement != null)
            {
                e.Handled = true;

                uiElement.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                //checkData();
            }
            */

            checkData();
        }

        private void buttonSave_alternative_Click(object sender, RoutedEventArgs e)
        {
            if (saveToDatabase() == -1)
                MessageBox.Show("Saving failed for some reason");

            fillListWithDummyRecords();
        }

        private void labelVin_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            MessageBox.Show(textBoxVin.Text.Substring(0, 5) + " " + textBoxVin.Text.Substring(5, 4) + " " + textBoxVin.Text.Substring(9, 4) + " " + textBoxVin.Text.Substring(13, 4));
        }

        private void buttonDateMakeitToday(object sender, RoutedEventArgs e)
        {
            dataChanged();
            dateInvoice.SelectedDate = DateTime.Today;
        }

        private void dataGridServices_Error(object sender, ValidationErrorEventArgs e)
        {
            MessageBox.Show("err");
        }

        private void textBoxNotes_TextChanged(object sender, TextChangedEventArgs e)
        {
            dataChanged();
        }

        private void dateInvoice_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            dataChanged();
        }

    }
}
