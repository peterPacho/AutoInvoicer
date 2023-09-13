using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using TextAlignment = iText.Layout.Properties.TextAlignment;

namespace AutoInvoicer
{


    public class PdfGenerate
    {


        //converts yyyymmdd int date to mm/dd/yyyy string date
        public static string intDateToString(int? date)
        {
            if (date == null) return "";

            string dateToReturn = "";

            try
            {
                //right now in yyyymmdd format, convert to mmddyyyy
                dateToReturn = Convert.ToString(date);

                //now should be in yyyymmddyyyy format
                dateToReturn = dateToReturn.Insert(8, dateToReturn.Substring(0, 4));

                //now should be in mmddyyyy
                dateToReturn = dateToReturn.Substring(4, dateToReturn.Length - 4);

                dateToReturn = dateToReturn.Insert(2, "/");
                dateToReturn = dateToReturn.Insert(5, "/");

                //removes leading 0 from month
                if (dateToReturn.IndexOf("0") == 0) dateToReturn = dateToReturn.Substring(1, dateToReturn.Length - 1);
            }
            catch { return ""; }
            


            return "Date: " + dateToReturn + "\n";
        }



        //used to open just generated pdf file
        public static void OpenWithDefaultProgram(string path)
        {
            Process fileopener = new Process();
            fileopener.StartInfo.FileName = "explorer";
            fileopener.StartInfo.Arguments = "\"" + path + "\"";
            fileopener.Start();
        }


        //returns input data with specified prefix and ends with page break if input data is not null, otherwise doesn't returns anything
#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
        public static string ifnotnull(string? data, string prefix)
#pragma warning restore CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
        {
            if (data == null || data == "") return "";

            data = data.Replace(" ", "\u00a0");

            return prefix + data + "\n";
        }

        public static string ifnotnull(int? data, string prefix)
        {
            if (data == null || data == 0) return "";


            string mileage = Convert.ToString(data);

            if (mileage.Length > 3) mileage = mileage.Insert(mileage.Length - 3, ",");


            return prefix + mileage + "\n";
        }
        public static string ifnotnull(double? data, string prefix)
        {
            if (data == null) return "";


            return prefix + Convert.ToString(data) + "\n";
        }
        public static double ifnotnull(double? data)
        {
            if (data == null) return 0;

            return Convert.ToDouble(data);
        }

        public static string twoDigits(int number)
        {
            if (number < 10) return $"0{number}";

            return $"{number}";
        }




        public static void generateInvoice(Invoice invoiceToPrint)
        {
            //generate filename
            string pdfFileName = "";
            DateTime currentDate = DateTime.Now;

            pdfFileName += $"{currentDate.Year}.{twoDigits(currentDate.Month)}.{twoDigits(currentDate.Day)}_{twoDigits(currentDate.Hour)}.{twoDigits(currentDate.Minute)}.{twoDigits(currentDate.Second)}_{currentDate.Millisecond}.pdf";

            try
            {
                Directory.CreateDirectory("./PDFs/");
            }

            catch (Exception e)
            {
                MessageBox.Show("Can't create directory 'PDFs' at the program root\n\n" + e);
            }
            

            pdfFileName = pdfFileName.Insert(0, "PDFs/");

            string shopInfo = Properties.Settings.Default.ShopInfo;

            PdfWriter writer = new PdfWriter($"./{pdfFileName}");


            PdfDocument pdf = new PdfDocument(writer);
            Document document = new Document(pdf);

            

            PdfFont f = PdfFontFactory.CreateFont();


            if (File.Exists("./calibri-regular.ttf"))
                f = PdfFontFactory.CreateFont("calibri-regular.ttf");
            else
            {
                MessageBox.Show("Font 'calibri-regular.ttf' not found. Using default font for PDF generation.");
            }

            document.SetFont(f);


            int margin = 30;
            document.SetMargins(margin, margin, margin, margin);




            Table table = new Table(2, true).UseAllAvailableWidth();
                table.SetMarginTop(0);

            //shop info in left top corner
            Cell cell = new Cell(1, 1).Add(new Paragraph(shopInfo));
            cell.SetTextAlignment(TextAlignment.LEFT)
                .SetPadding(5)
                .SetBorder(iText.Layout.Borders.Border.NO_BORDER)
                .SetFontSize(10)
                ;
            table.AddCell(cell);


            //date and invoice number in right top corner
            if(Properties.Settings.Default.DontPrintInvoiceID)
                cell = new Cell().Add(new Paragraph($"{intDateToString(invoiceToPrint.invoiceDate)}"));
            else
                cell = new Cell().Add(new Paragraph($"{intDateToString(invoiceToPrint.invoiceDate)}Invoice #{invoiceToPrint.id}"));

            cell.SetTextAlignment(TextAlignment.RIGHT)
                .SetPadding(5)
                .SetBorder(iText.Layout.Borders.Border.NO_BORDER)
                .SetFontSize(14);
            table.AddCell(cell);

            document.Add(table);











            int tableWidth = 500;

            //add customer info on left
            table = new Table(2, true).UseAllAvailableWidth();
            table.SetMarginTop(15);

            cell = new Cell(1, 1).Add(new Paragraph("Customer info"));
            cell.SetTextAlignment(TextAlignment.CENTER)
                .SetPadding(5)
                .SetWidth(tableWidth)
                .SetBorderTop(iText.Layout.Borders.Border.NO_BORDER)
                .SetBorderLeft(iText.Layout.Borders.Border.NO_BORDER)
                ;
            table.AddCell(cell);


            //date and invoice number in right top corner
            cell = new Cell(1,1).Add(new Paragraph("Vehicle info"));
            cell.SetTextAlignment(TextAlignment.CENTER)
                .SetPadding(5)
                .SetBorderTop(iText.Layout.Borders.Border.NO_BORDER)
                .SetBorderRight(iText.Layout.Borders.Border.NO_BORDER)
                .SetWidth(tableWidth);
            table.AddCell(cell);


            table.StartNewRow();

            cell = new Cell(1, 1).Add(new Paragraph(invoiceToPrint.customer));
            cell.SetTextAlignment(TextAlignment.LEFT)
                .SetPadding(5)
                .SetBorderBottom(iText.Layout.Borders.Border.NO_BORDER)
                .SetBorderLeft(iText.Layout.Borders.Border.NO_BORDER)
                .SetWidth(tableWidth)
            ;
            table.AddCell(cell);
            cell = new Cell(1, 1).Add(new Paragraph(  ifnotnull(invoiceToPrint.vehicleInfo, "") + ifnotnull(invoiceToPrint.vin, "") + ifnotnull(invoiceToPrint.mileage, "Mileage: ") ));
            cell.SetTextAlignment(TextAlignment.LEFT)
                .SetPadding(5)
                .SetBorderBottom(iText.Layout.Borders.Border.NO_BORDER)
                .SetBorderRight(iText.Layout.Borders.Border.NO_BORDER)
                .SetWidth(tableWidth);
            table.AddCell(cell);

            document.Add(table);





            table = new Table(8, true).UseAllAvailableWidth();
            table.SetMarginTop(5);

            cell = new Cell(1, 7).Add(new Paragraph("Description"));
            cell.SetTextAlignment(TextAlignment.LEFT)
                .SetPadding(5)
                .SetBorderTop(iText.Layout.Borders.Border.NO_BORDER)
                .SetBorderLeft(iText.Layout.Borders.Border.NO_BORDER)
                .SetWidth(tableWidth)
            ;
            table.AddCell(cell);

            cell = new Cell(1, 6).Add(new Paragraph("Amount"));
            cell.SetTextAlignment(TextAlignment.RIGHT)
                .SetPadding(5)
                .SetBorderTop(iText.Layout.Borders.Border.NO_BORDER)
                .SetBorderRight(iText.Layout.Borders.Border.NO_BORDER)
                .SetWidth(tableWidth);
            table.AddCell(cell);

            table.StartNewRow();


            double totalAmount = 0;
            int totalAmountCounter = 0;

            foreach(Service singleService in invoiceToPrint.services)
            {


                cell = new Cell(1, 7).Add(new Paragraph(ifnotnull(singleService.service, "")));
                cell.SetTextAlignment(TextAlignment.LEFT)
                    .SetPadding(5)
                    .SetBorderTop(iText.Layout.Borders.Border.NO_BORDER)
                    .SetBorderLeft(iText.Layout.Borders.Border.NO_BORDER)
                    .SetBorderBottom(iText.Layout.Borders.Border.NO_BORDER)
                    .SetWidth(tableWidth)
                ;
                table.AddCell(cell);

                //if cost negative, print - in front of $ sign, not after

                if (singleService.cost < 0)
                {
                    double cost = Math.Abs(ifnotnull(singleService.cost));
                    cell = new Cell(1, 6).Add(new Paragraph("- $" + Convert.ToString(cost)));
                }
                        
                else
                        cell = new Cell(1, 6).Add(new Paragraph(ifnotnull(singleService.cost, "$")));

                cell.SetTextAlignment(TextAlignment.RIGHT)
                    .SetPadding(5)
                    .SetBorderTop(iText.Layout.Borders.Border.NO_BORDER)
                    .SetBorderBottom(iText.Layout.Borders.Border.NO_BORDER)
                    .SetBorderRight(iText.Layout.Borders.Border.NO_BORDER)
                    .SetWidth(tableWidth);
                table.AddCell(cell);

                table.StartNewRow();



                totalAmount += ifnotnull(singleService.cost);

                if (singleService.cost != null)
                    totalAmountCounter++;
                
            }

            document.Add(table);




            //print total only if more than one cost in entered

            if (totalAmountCounter > 1)
            {
                table = new Table(1, true).UseAllAvailableWidth();
                cell = new Cell(1, 1).Add(new Paragraph($"Total: ${Math.Round(totalAmount, 2)}"));
                cell.SetTextAlignment(TextAlignment.RIGHT)
                    .SetPadding(5)
                    .SetBorder(iText.Layout.Borders.Border.NO_BORDER)
                    .SetFontSize(18)
                    .SetWidth(tableWidth)
                ;
                table.AddCell(cell);

                document.Add(table);
            }


            





            document.Close();

            OpenWithDefaultProgram(pdfFileName.Replace("/", "\\"));
        }
    }
}
