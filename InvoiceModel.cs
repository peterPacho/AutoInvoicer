using System;
using System.Data;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;

#nullable enable

namespace AutoInvoicer
{

    /*
     * 
     *          This class contains data model for single invoice record, and all services belonging to that invoice.
     *          
     */
    public class Invoice
    {

        public int id { get; set; } //main/Invoices/Id in SQlite                - unique key for each invoice
        public string? vin { get; set; } //main/Invoices/VIN                    - just a vehicle vin number in text format, can be null
        public string? vehicleInfo { get; set; } //main/Invoices/VehicleInfo    - just vehicle info, can be null
        public int? mileage { get; set; } //main/Invoices/Mileage               - mileage in int format, can be null
        public string? customer { get; set; } //main/Invoices/CustomerID          - that field refers to table Customers
        public int? invoiceDate { get; set; } //main/Invoices/InvoiceDate        - date format in int, in yyyymmdd format
        public string? notes { get; set; }
        public int matchesFound { get; set; } //used for single word search, to sort the list

        public string? customerSingleLine //for datagrid data binding - displays all info in one line without any page breaks
        {
            get 
            {
                if (customer != null)
                {
                    string temp = customer.Replace(System.Environment.NewLine, " ");
                    temp = temp.Replace('\n', ' ');
                    temp = temp.Replace("  ", " ");
                    return temp;
                }

                return null; 
            }
        }

        public string? invoiceDateFormatted //for datagrid data binding
        {
            get
            {
                if (invoiceDate != null)
                {
                    string temp = invoiceDate.ToString();
                    if (temp == null) return null;

                    temp = temp.Insert(4, "/");
                    temp = temp.Insert(7, "/");
                    return temp;
                }

                return null;
            }
        }

        public List<Service>? services { get; set; }
    }


    public class Customers
    {
        public int id { get; set; } //main/Customers/Id
        public string? customerData { get; set; }//main/Customers/CustomerData

        public Customers(int id, string? customerData)
        {
            this.id = id;
            this.customerData = customerData;
        }

        public Customers()
        {

        }
    }

    public class Service : INotifyPropertyChanged
    {
        public int id { get; set; } //main/Services/Id

        private string? _service;
        public string? service
        {
            get
            {
                if (this._service == null)
                    return "";

                return this._service;
            }
            set
            {
                this._service = value;
                NotifyPropertyChanged("service");
            }

        } //main/Services/Service
        public double? cost { get; set; }

        public string? costGrid
        {
            get
            {
                if (cost == null) return null;
                return cost.ToString(); 
            }
            set 
            {
                if (value == null)
                {
                    cost = null;
                }
                else
                {

                    double val;

                    if (double.TryParse(value, out val))
                    {
                        cost = val;
                    }
                    else
                    {
                        try
                        {
                            //https://stackoverflow.com/questions/333737/evaluating-string-342-yield-int-18
                            DataTable dt = new DataTable();
                            var result = dt.Compute(value, "");
                            cost = Convert.ToDouble(result);
                        }
                        catch
                        {
                            cost = null;
                        }
                    }
                }
            }
        }

        public int invoiceID { get; set; } //main/Services/InvoiceID - that id ties the record to the Invoice, one invoice can have multiple service records

        public event PropertyChangedEventHandler? PropertyChanged;

        // This method is called by the Set accessor of each property.  
        // The CallerMemberName attribute that is applied to the optional propertyName  
        // parameter causes the property name of the caller to be substituted as an argument.  
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

}
