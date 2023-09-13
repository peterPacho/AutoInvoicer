using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SQLite;
using System.Data;
using Dapper;
using System.Linq;
using System.Windows;
using System.IO;
using System.IO.Compression;
using MaterialDesignThemes.Wpf;
using Microsoft.VisualBasic;
using Org.BouncyCastle.Utilities.Collections;

namespace AutoInvoicer
{
    public class SqliteDataAccess
    {
        //this method loads all entries from Customers database
        public static List<Customers> LoadCustomers(ref bool failedLoading)
        {
            //this entry makes sure that file is closed properly even on crash
            using (IDbConnection dbConn = new SQLiteConnection(LoadConnectionString()))
            {
                try
                {
                    //uses Dapper to process SQL command
                    var output = dbConn.Query<Customers>("select * from Customers", new DynamicParameters());
                    return output.ToList();
                }
                catch
                {
                    failedLoading = true;
                }

            }

            return new List<Customers>(); //return empty if something wrong

        }


        //this method loads all entries from Invoices Database
        public static List<Invoice> LoadInvoices(ref bool failedLoading)
        {
            //this entry makes sure that file is closed properly even on crash
            using (IDbConnection dbConn = new SQLiteConnection(LoadConnectionString()))
            {
                try
                {
                    //uses Dapper to process SQL command
                    var output = dbConn.Query<Invoice>("select * from Invoices", new DynamicParameters());
                    List<Invoice> invoices = output.ToList();

                    //there's no need to load services at this time
                    //for each invoice loaded up, find services connected to it
                    //foreach (Invoice invoice in invoices) 
                    //{
                    //    var foundServices = LoadServices(ref failedLoading, invoice.id);
                    //    invoice.services = foundServices.ToList();

                    //    if (failedLoading) return new List<Invoice>();
                    //}

                    return invoices;
                }
                catch
                {
                    failedLoading = true;
                }

            }

            return new List<Invoice>(); //return empty if something wrong

        }


        //same as above but used for searching for specific invoice
        public static List<Invoice> LoadInvoices(ref bool failedLoading, string textBoxContent)
        {
            //textBoxContent might contain multiple search "words", so put each "word" in a separate item
            while (textBoxContent.Contains("  "))
                textBoxContent = textBoxContent.Replace("  ", " ");
            string[] words = textBoxContent.Split(' ');

            //temporary list of invoices
            List<Invoice> invoices = new List<Invoice>();
            List<Service> services = new List<Service>();

            bool searchServices = Properties.Settings.Default.NewSearchServices;

            //load all invoices to the list
            using (IDbConnection dbConn = new SQLiteConnection(LoadConnectionString()))
            {
                try
                {
                    //load all invoices
                    invoices = dbConn.Query<Invoice>("select * from Invoices", new DynamicParameters()).ToList();

                    if (searchServices) services = dbConn.Query<Service>("select * from Services", new DynamicParameters()).ToList();
                    
                }
                catch (Exception e)
                {
                    MessageBox.Show($"Failed to load data from the database.\n\n\n" + e);
                    failedLoading = true;
                }

            }

            //cycle through each item and check for matches
            foreach (Invoice singleInvoice in invoices)
            {
                int containsCounter = 0;

                //check for each word if its found in the 
                foreach (string singleWord in words)
                {
                    //build one long string to search for matches
                    string sumOfDataInInvoice = singleInvoice.vehicleInfo + " " + singleInvoice.vin + " " + singleInvoice.customer + " " + Convert.ToString(singleInvoice.id) + " " + singleInvoice.notes;

                    //if searching through services as well, then find all services with same ID and add them to the string
                    if (searchServices)
                    {

                        foreach (Service singleService in services)
                        {
                            if (singleService.invoiceID == singleInvoice.id) sumOfDataInInvoice += singleService.service;
                        }

                    }


                    //if any found, count
                    if (sumOfDataInInvoice.IndexOf(singleWord, StringComparison.OrdinalIgnoreCase) > 0 || sumOfDataInInvoice.Contains(singleWord))
                    {
                        containsCounter++;
                    }

                }

                //write how many "matches" found in the row
                singleInvoice.matchesFound = containsCounter;
            }


            //if searching for multiple words, we don't want invoices on the list that have 0 or too few matches
            int matchesToExclude = words.Count() - 2;
            if (Properties.Settings.Default.NewSearchMustMatchAll)
                matchesToExclude = words.Count() - 1;

            if (matchesToExclude < 0) matchesToExclude = 0;


            //remove all records that have 0 matches
            invoices.RemoveAll(InvoiceSearch => InvoiceSearch.matchesFound <= matchesToExclude);


            //at this point return the list, sorting by amount of matches is done elsewhere
            return invoices; //return empty if something wrong
        }


        public static Invoice LoadSingleInvoice(ref bool failedLoading, int id)
        {
            //this entry makes sure that file is closed properly even on crash
            using (IDbConnection dbConn = new SQLiteConnection(LoadConnectionString()))
            {
                try
                {
                    //uses Dapper to process SQL command
                    var output = dbConn.Query<Invoice>("select * from Invoices where ID=" + id.ToString(), new DynamicParameters());
                    List<Invoice> invoices = output.ToList();

                    //for each invoice loaded up, find services connected to it
                    foreach (Invoice invoice in invoices)
                    {
                        var foundServices = LoadServices(ref failedLoading, invoice.id);
                        invoice.services = foundServices.ToList();

                        if (failedLoading) return null;
                    }

                    return invoices.ElementAt(0);
                }
                catch
                {
                    failedLoading = true;
                }

            }

            return null;
        }

        //this method loads all entries from Services database
        public static List<Service> LoadServices(ref bool failedLoading, int id)
        {
            //this entry makes sure that file is closed properly even on crash
            using (IDbConnection dbConn = new SQLiteConnection(LoadConnectionString()))
            {
                try
                {
                    //uses Dapper to process SQL command
                    var output = dbConn.Query<Service>("select * from Services where InvoiceID=" + id.ToString(), new DynamicParameters());
                    return output.ToList();
                }
                catch
                {
                    failedLoading = true;
                }

            }

            return new List<Service>(); //return empty if something wrong
        }


        //having ' inside the query is problematic, so this function finds all ' and adds one ' to each, so 
        public static string insertFilter(string str)
        {
            if (str == null) return "";

            return str.Replace("'", "''");
        }

        public static string insertFilter(int? value)
        {
            if (value == null)
                return "NULL";

            return Convert.ToString(value);
        }
        public static string insertFilter(double? value)
        {
            if (value == null)
                return "NULL";

            return Convert.ToString(value);
        }

        public static void createDatabaseBackup()
        {
            string zipPath = @"./Backup.zip";


            //get backup archive info
            try
            {
                if (File.Exists(zipPath))
                {
                    int numberOfElementsInArchive = 0;

                    using (ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Read))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            if (!string.IsNullOrEmpty(entry.Name))
                                numberOfElementsInArchive++;
                        }

                    }

                    //if over X elements inside the archive, make a copy and start new archive
                    if (numberOfElementsInArchive > 10)
                    {
                        //if copy already exists, temporarly move it
                        if (File.Exists(@"./Backup.zip.old"))
                        {
                            File.Move(@"./Backup.zip.old", @"./Backup.zip.old2");
                        }

                        File.Move(zipPath, "./Backup.zip.old");

                        if (File.Exists(@"./Backup.zip.old2"))
                        {
                            File.Delete(@"./Backup.zip.old2");
                        }
                    }

                }
            }

            catch
            { }

            
            


            try
            {
                DateTime currentDate = DateTime.Now;

                
                string fileName = $"{currentDate.Year}.{PdfGenerate.twoDigits(currentDate.Month)}.{PdfGenerate.twoDigits(currentDate.Day)}_{PdfGenerate.twoDigits(currentDate.Hour)}.{PdfGenerate.twoDigits(currentDate.Minute)}.{PdfGenerate.twoDigits(currentDate.Second)}_{currentDate.Millisecond}.db";

                using (ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Update))
                {
                    archive.CreateEntryFromFile("./Invoices.db", fileName);
                }

            }
            catch
            {
                MessageBox.Show("Creating database backup failed for some reason");
            }

        }


        //creates another .zip file in custom location, but updates it only once per day
        public static void createDatabaseBackupInCustomLocation()
        {
            DateTime currentDate = DateTime.Now;
            string todaysDate = $"{currentDate.Year}.{PdfGenerate.twoDigits(currentDate.Month)}.{PdfGenerate.twoDigits(currentDate.Day)}";
            string newBackupName = $"{currentDate.Year}.{PdfGenerate.twoDigits(currentDate.Month)}.{PdfGenerate.twoDigits(currentDate.Day)}_{PdfGenerate.twoDigits(currentDate.Hour)}.{PdfGenerate.twoDigits(currentDate.Minute)}.{PdfGenerate.twoDigits(currentDate.Second)}_{currentDate.Millisecond}.db";



            //if custom location specified, create different kind of backup file there
            if (Properties.Settings.Default.CustomBackupLocation != "" && Properties.Settings.Default.CustomBackupLastUpdated != todaysDate)
            {

                //get backup archive info
                try
                {
                    if (File.Exists(Properties.Settings.Default.CustomBackupLocation))
                    {
                        int numberOfElementsInArchive = 0;

                        ZipArchive archive = ZipFile.Open(Properties.Settings.Default.CustomBackupLocation, ZipArchiveMode.Update);
                        
                        foreach (var entry in archive.Entries)
                        {
                            if (!string.IsNullOrEmpty(entry.Name))
                                numberOfElementsInArchive++;
                        }

                        

                        //if over X elements inside the archive, extract it, remove last element, and create new zip
                        if (numberOfElementsInArchive > 15)
                        {
                            archive.ExtractToDirectory(@"./TempCustomBackup/", true);

                            DirectoryInfo backupDirectory = new DirectoryInfo(@"./TempCustomBackup/");
                            FileInfo[] backupFileList = backupDirectory.GetFiles();
                            //var backupFileListSorted = backupFileList.OrderByDescending(File => File.Name);

                            //backupFileList should be sorted by name, with oldest files appearing first, so simply delete first file
                            backupFileList[0].Delete();

                            //now copy current database to the location
                            File.Copy(@"./Invoices.db", (@"./TempCustomBackup/") + newBackupName, true);

                            //now delete old directory
                            archive.Dispose();
                            File.Delete(Properties.Settings.Default.CustomBackupLocation);

                            //now make a zip out of it and copy to custom location
                            ZipFile.CreateFromDirectory(@"./TempCustomBackup/", Properties.Settings.Default.CustomBackupLocation);

                            Directory.Delete(@"./TempCustomBackup/", true);
                        }

                        //if under, simply add file to the archive
                        else
                        {
                            archive.CreateEntryFromFile(@"./Invoices.db", newBackupName);
                        }


                        archive.Dispose();
                    }

                    else 
                    {
                        using (ZipArchive archive = ZipFile.Open(Properties.Settings.Default.CustomBackupLocation, ZipArchiveMode.Update))
                        {
                            archive.CreateEntryFromFile(@"./Invoices.db", newBackupName);
                        }

                    }
                }

                catch
                {
                    MessageBox.Show("Creating backup file in custom location failed!");
                }



                //save last time we updated the custom file
                Properties.Settings.Default.CustomBackupLastUpdated = todaysDate;
                Properties.Settings.Default.Save();
            }

        }


        public static int SaveInvoice(Invoice invoice)
        {
            //any time we are saving something to the database, make a backup first
            createDatabaseBackup();

            //this entry makes sure that file is closed properly even on crash
            using (IDbConnection dbConn = new SQLiteConnection(LoadConnectionString()))
            {
                string query = "";
                string randomStringToReplaceLater = "sd9873246hdjkhgh12c-=109kc1x5123564586423]p"; //I mean there is small chance somebody will type exactly that instead of vin number hah

                //if adding new invoice, use some random string instead of vin that we will search for later to find out newly created ID
                //there might be a better method for doing that, but getting ID of just created record got better of me and this workaroun is the best I could come up with
                //help it's getting kinda late
                if (invoice.id == -1)
                {
                    query = $"INSERT INTO Invoices(VIN,VehicleInfo,Mileage,Customer,InvoiceDate, Notes) VALUES ('" + 
                        $"{randomStringToReplaceLater}','{insertFilter(invoice.vehicleInfo)}',{invoice.mileage},'{insertFilter(invoice.customer)}',{insertFilter(invoice.invoiceDate)}, '{insertFilter(invoice.notes)}');";

                    try
                    {
                        dbConn.Execute(query);

                    }
                    catch (Exception e)
                    {
                        MessageBox.Show($"Failed to insert new data into Invoices table!\n\n" + e); return -1;
                    }

                    //now we need to search for our "special" string
                    try
                    {
                        var tempInvoiceJustToGetIdFrom = dbConn.Query<Invoice>($"SELECT * FROM Invoices WHERE VIN LIKE '{randomStringToReplaceLater}'", new DynamicParameters()).ToList();
                        var lastInvoiceFound = tempInvoiceJustToGetIdFrom.Last(); //should be only one but let's play safe
                        invoice.id = lastInvoiceFound.id; //now we have newly created ID
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show("Failed to find just created new invoice.\n\n" + e); return -1;
                    }

                   
                    try
                    {
                        dbConn.Execute($"UPDATE Invoices SET VIN='{insertFilter(invoice.vin)}' WHERE ROWID={invoice.id}"); //set correct vin
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show("Failed to update newly created invoice with proper VIN number.\n\n" + e); return -1;
                    }

                }

                else
                {
                    query = $"UPDATE Invoices SET " +
                        $"VIN='{insertFilter(invoice.vin)}', " +
                        $"VehicleInfo='{insertFilter(invoice.vehicleInfo)}', " +
                        $"Mileage={invoice.mileage}, " +
                        $"Customer='{insertFilter(invoice.customer)}', " +
                        $"InvoiceDate={insertFilter(invoice.invoiceDate)}, " +
                        $"Notes='{insertFilter(invoice.notes)}'" +
                        $"WHERE Id={invoice.id}";


                    try
                    {
                        dbConn.Execute(query);
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show($"Failed to update new invoice with ID {invoice.id}" + e); return -1;
                    }
                }



                //take care of list of services, first delete all exisitng data fro the database
                try
                {
                    dbConn.Execute($"DELETE FROM Services WHERE InvoiceID={invoice.id}");

                }
                catch { return -1; }

                //send all services to database
                foreach (Service singleServiceRecord in invoice.services)
                {
                    query = $"INSERT INTO Services(Service, Cost, InvoiceID) VALUES ('{insertFilter(singleServiceRecord.service)}',{insertFilter(singleServiceRecord.cost)},{invoice.id})";

                    try
                    {
                        dbConn.Execute(query);
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show($"Failed to insert new service record.\n\nService desc:{singleServiceRecord.service}\nService cost:{singleServiceRecord.cost}\nInvoice ID: {invoice.id}\n\n\n" + e);
                        return -1;
                    }
                }
            }


            createDatabaseBackupInCustomLocation();


            return invoice.id;
        }


        public static void DeleteInvoice(int id)
        {
            createDatabaseBackup();

            using (IDbConnection dbConn = new SQLiteConnection(LoadConnectionString()))
            {
                try
                {
                    dbConn.Execute($"DELETE FROM Invoices WHERE Id={id}");
                    dbConn.Execute($"DELETE FROM Services WHERE InvoiceID={id}");
                }
                catch (Exception e)
                {
                    MessageBox.Show($"Failed to delete records with ID {id} from the database!\n\n" + e);
                }
                
            }
        }



        public static bool SaveCustomer(Customers customerData)
        {
            createDatabaseBackup();

            string query = "";

            if (customerData.id == -1)
                query = $"INSERT INTO Customers('CustomerData') VALUES ('{insertFilter(customerData.customerData)}');";
            else
                query = $"UPDATE Customers SET CustomerData='{insertFilter(customerData.customerData)}' WHERE ROWID={customerData.id}";

            using (IDbConnection dbConn = new SQLiteConnection(LoadConnectionString()))
            {
                try
                {
                    dbConn.Execute(query);
                }
                catch (Exception e)
                {
                    MessageBox.Show($"Failed to update customer records with ID {customerData.id} from the database!\n\n" + e);
                    return false;
                }

            }

            return true;
        }


        public static void DeleteCustomer(int id)
        {
            createDatabaseBackup();

            using (IDbConnection dbConn = new SQLiteConnection(LoadConnectionString()))
            {
                try
                {
                    dbConn.Execute($"DELETE FROM Customers WHERE Id={id}");
                }
                catch (Exception e)
                {
                    MessageBox.Show($"Failed to delete customer with ID {id} from the database!\n\n" + e);
                }

            }
        }


        public static void createNewFile()
        {
            if(File.Exists("Invoices.db"))
            {
                MessageBox.Show("Database file already exists!");
                return;
            }

            SQLiteConnection.CreateFile("Invoices.db");

            SQLiteConnection dbConn = new SQLiteConnection(LoadConnectionString());

            dbConn.Open();

            SQLiteCommand command = new SQLiteCommand("CREATE TABLE \"Customers\" (\"Id\" INTEGER NOT NULL UNIQUE,\"CustomerData\" TEXT, PRIMARY KEY(\"Id\" AUTOINCREMENT))", dbConn);
            command.ExecuteNonQuery();

            command = new SQLiteCommand("CREATE TABLE \"Invoices\" (\"Id\" INTEGER NOT NULL UNIQUE,\"VIN\" TEXT, \"VehicleInfo\" TEXT, \"Mileage\" INTEGER, \"Customer\" TEXT, \"InvoiceDate\" INTEGER, \"Notes\" TEXT, PRIMARY KEY(\"Id\" AUTOINCREMENT))", dbConn);
            command.ExecuteNonQuery();

            command = new SQLiteCommand("CREATE TABLE \"Services\" (\"Id\" INTEGER NOT NULL UNIQUE, \"Service\" TEXT, \"Cost\" REAL, \"InvoiceID\" INTEGER, PRIMARY KEY(\"Id\" AUTOINCREMENT))", dbConn);
            command.ExecuteNonQuery();

            //command = new SQLiteCommand("CREATE TABLE sqlite_sequence(name,seq)", dbConn);
            //command.ExecuteNonQuery();
            //UPDATE sqlite_sequence SET seq = 1000 WHERE name = \"Invoices\"

            command = new SQLiteCommand("INSERT INTO sqlite_sequence('name','seq') VALUES (\"Invoices\", 999)", dbConn);
            command.ExecuteNonQuery();

            dbConn.Close();
        }


        //that pulls up info about database location from App.config file
        private static string LoadConnectionString(string id = "Default")
        {
            return $"Data Source='Invoices.db';Version=3;";
        }
    }
}
