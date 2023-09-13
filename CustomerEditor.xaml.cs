using System;
using System.Collections.Generic;
using System.Text;
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
    /// <summary>
    /// Interaction logic for CustomerEditor.xaml
    /// </summary>
    public partial class CustomerEditor : Window
    {

        public Customers customerToEdit;

        public CustomerEditor(Customers customer = null)
        {
            InitializeComponent();

            //if creating new customer, don't need delete button
            if (customer == null)
            {
                buttonDelete.Visibility = Visibility.Hidden;
                customerToEdit = new Customers();
                customerToEdit.id = -1;
                customerToEdit.customerData = "";
            }
            else 
            {
                customerToEdit = customer;
            }

            textBoxCustomerInfo.Text = customerToEdit.customerData;
        }

        private void buttonCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void buttonDelete_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure that you want to delete this customer record?\n\nThis operation cannot be undone!", "Deleting customer information", MessageBoxButton.YesNo);

            if (result == MessageBoxResult.Yes)
            {
                SqliteDataAccess.DeleteCustomer(customerToEdit.id);
                this.Close();
            }


            
        }

        private void buttonSave_Click(object sender, RoutedEventArgs e)
        {
            customerToEdit.customerData = textBoxCustomerInfo.Text;

            if (SqliteDataAccess.SaveCustomer(customerToEdit))
                this.Close();
            
        }
    }
}
