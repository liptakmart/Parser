using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Parser
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<VoucherRow> allLinesList = new List<VoucherRow>();
        private IDictionary<string, List<VoucherRow>> dupliciteVouchersDictionary = new Dictionary<string, List<VoucherRow>>();
        private List<VoucherRow> dupliciteLines = new List<VoucherRow>();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void onNewClick(object sender, RoutedEventArgs e)
        {
            try
            {
                this.originTb.Text = "";
                this.allLinesList = new List<VoucherRow>();
            }
            catch (Exception)
            {
                System.Windows.Application.Current.Shutdown();
            }
        }

        private void onOpenClick(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Filter = "Csv súbor (*.csv)|*.csv";
                if (openFileDialog.ShowDialog() == true)
                {
                    string fileName = openFileDialog.FileName;
                    ReadCsvData readCsvFile = this.ReadCSV(fileName);

                    this.allLinesList = readCsvFile.AllLines;
                    this.dupliciteVouchersDictionary = readCsvFile.DupliciteVouchersDictionary;
                    this.dupliciteLines = readCsvFile.DupliciteLines;

                    originTb.Text = $"Počet riadkov v dokumente: {readCsvFile.AllLines.Count} \nPočet nájdených duplicitných kódov fliaš (unikátnych): {readCsvFile.DupliciteVouchersDictionary.Count}\n";
                    List<ResultInfoDataGridLine> resultInfoList = new List<ResultInfoDataGridLine>();

                    int counter = 1;
                    string cacheCode = "";
                    for (int i = 0; i < this.dupliciteLines.Count; i++)
                    {
                        ResultInfoDataGridLine dgLine = new ResultInfoDataGridLine();

                        if (cacheCode != this.dupliciteLines[i].voucher_code && cacheCode != "")
                        {
                            counter++;
                        }
                        dgLine.voucherCode = this.dupliciteLines[i].voucher_code;
                        dgLine.ammount = Math.Truncate(double.Parse(this.dupliciteLines[i].voucher_amount, System.Globalization.CultureInfo.InvariantCulture) * 100) / 100;
                        dgLine.number = counter;
                        if (this.dupliciteVouchersDictionary.ContainsKey(this.dupliciteLines[i].voucher_code))
                        {
                            List<VoucherRow> lines = this.dupliciteVouchersDictionary[this.dupliciteLines[i].voucher_code];
                            if (this.AreLinesFraud(lines))
                            {
                                dgLine.potentialFraud = "Upozornenie!";
                            }
                        }

                        resultInfoList.Add(dgLine);
                        cacheCode = this.dupliciteLines[i].voucher_code;
                    }

                    resultInfoGrid.ItemsSource = resultInfoList;
                    resultInfoGrid.Columns[0].Header = "Čís.";
                    resultInfoGrid.Columns[1].Header = "Kód flaše";
                    resultInfoGrid.Columns[2].Header = "Záloha";
                    resultInfoGrid.Columns[3].Header = "Podozrivá činnosť";
                    this.FireInfoDialog("Info", "Nahratý súbor bol úspešne spracovaný. Kliknite Súbor->Uložiť pre uloženie.", MessageBoxImage.Information);
                }
            }
            catch (Exception)
            {
                System.Windows.Application.Current.Shutdown();
            }
        }

        private void onSaveClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (this.allLinesList.Count == 0)
                {
                    return;
                }
                SaveFileDialog saveFileDialog = new SaveFileDialog();
                saveFileDialog.Filter = "Csv súbor (*.csv)|*.csv";
                if (saveFileDialog.ShowDialog() == true)
                {
                    using (var w = new StreamWriter(saveFileDialog.FileName))
                    {
                        string headerLine = "cp_code;voucher_code;voucher_amount;voucher_redemption_date;month;year;charity";
                        w.WriteLine(headerLine);
                        w.Flush();

                        //Write duplicite
                        foreach (var item in this.dupliciteLines)
                        {
                            //w.WriteLine(item.Value[0].toCsvString());
                            w.WriteLine(item.toCsvString());
                        }

                        w.WriteLine("\n");
                        w.Flush();
                        w.WriteLine("\n");
                        w.Flush();

                        //Write others (removed duplicities)
                        foreach (VoucherRow item in this.allLinesList)
                        {
                            if (!this.dupliciteVouchersDictionary.ContainsKey(item.voucher_code)){
                                w.WriteLine(item.toCsvString());
                            }
                        }

                        //foreach (VoucherRow line in this.dupliciteLines)
                        //{
                        //    w.WriteLine(line.toCsvString() + "\n");
                        //    w.Flush();
                        //}

                        /*
                        foreach (VoucherRow line in this.allLinesList)
                        {
                            if (!this.dupliciteVouchersDictionary.ContainsKey(line.voucher_code))
                            {
                                w.WriteLine(line.toCsvString());
                                w.Flush();
                            }
                        }
                        */

                        this.FireInfoDialog("Info", "Súbor úspešne uložený.", MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception)
            {
                System.Windows.Application.Current.Shutdown();
            }
        }

        private void onExitClick(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        public ReadCsvData ReadCSV(string fileName)
        {
            try
            {
                Dictionary<string, List<VoucherRow>> vouchersDictionary = new Dictionary<string, List<VoucherRow>>();
                List<VoucherRow> allVouchers = new List<VoucherRow>(50000);
                string[] stringLines = File.ReadAllLines(System.IO.Path.ChangeExtension(fileName, ".csv"));
                int counter = 0;
                foreach (var item in stringLines)
                {
                    if (counter == 0)
                    {
                        counter++;
                        continue;
                    }

                    string[] data = item.Split(';');
                    VoucherRow newVoucher = new VoucherRow(data[0], data[1], data[2], data[3], data[4], data[5], data[6]);
                    allVouchers.Add(newVoucher);
                    if (vouchersDictionary.ContainsKey(newVoucher.voucher_code))
                    {
                        vouchersDictionary[newVoucher.voucher_code].Add(newVoucher);
                    }
                    else
                    {
                        List<VoucherRow> list = new List<VoucherRow>();
                        list.Add(newVoucher);
                        vouchersDictionary.Add(newVoucher.voucher_code, list);
                    }
                    
                }

                vouchersDictionary = vouchersDictionary.Where(i => i.Value.Count > 1).ToDictionary( r => r.Key, r => r.Value);
                List<VoucherRow> dupliciteLines = new List<VoucherRow>();
                foreach (KeyValuePair<string, List<VoucherRow>> entry in vouchersDictionary)
                {
                    if (entry.Value.Count() > 1)
                    {
                        foreach (var item in entry.Value)
                        {
                            dupliciteLines.Add(item);
                        }
                    }
                }

                return new ReadCsvData(allVouchers, vouchersDictionary, dupliciteLines.OrderByDescending(i => i.voucher_code).ToList());
            }
            catch (Exception)
            {
                System.Windows.Application.Current.Shutdown();
            }

            return null;
        }

        /// <summary>
        /// Shows dialog
        /// </summary>
        /// <param name="caption"></param>
        /// <param name="text"></param>
        /// <param name="iconType"></param>
        private void FireInfoDialog(string caption, string text, MessageBoxImage iconType)
        {
            MessageBoxButton button = MessageBoxButton.OK;
            MessageBoxResult result;
            result = MessageBox.Show(text, caption, button, iconType, MessageBoxResult.Yes);
        }

        /// <summary>
        /// Returns if set of same vouchers are fraud
        /// </summary>
        /// <param name="vouchersSameCode">set of vouchers with same code</param>
        /// <returns></returns>
        private bool AreLinesFraud(List<VoucherRow> vouchersSameCode)
        {
            if (vouchersSameCode.Count < 2)
            {
                throw new Exception("Invalid use of this method");
            }

            //number of signs types (+-) must be same, or +1 in case of plus sign
            int plusCount = 0;
            int minusCount = 0;

            foreach (var line in vouchersSameCode)
            {
                if (double.Parse(line.voucher_amount, System.Globalization.CultureInfo.InvariantCulture) >= 0)
                {
                    plusCount++;
                }
                else
                {
                    minusCount++;
                }
            }

            if (plusCount == minusCount || plusCount == minusCount + 1)
            {
                return false;
            }
            else
            {
                return true;
            }
        }
    }

    /// <summary>
    /// Result line of datagrid
    /// </summary>
    public class ResultInfoDataGridLine
    {
        public int number { get; set; }
        public string voucherCode { get; set; }
        public double ammount { get; set; }
        public string potentialFraud { get; set; }
    }

    public class VoucherRow
    {
        public string cp_code { get; set; }
        public string voucher_code { get; set; }
        public string voucher_amount { get; set; }
        public string voucher_redemption_date { get; set; }
        public string month { get; set; }
        public string year { get; set; }
        public string charity { get; set; }
        public VoucherRow(
            string cp_code, 
            string voucher_code,
            string voucher_amount, 
            string voucher_redemption_date, 
            string month, 
            string year,
            string charity)
        {
            this.cp_code = cp_code;
            this.voucher_code = voucher_code;
            this.voucher_amount = voucher_amount;
            this.voucher_redemption_date = voucher_redemption_date;
            this.month = month;
            this.year = year;
            this.charity = charity;
        }

        public string toCsvString()
        {
            return $"{this.cp_code};{this.voucher_code};{this.voucher_amount};{this.voucher_redemption_date};{this.month};{this.year};{this.charity}";
        }
    }

    public class ReadCsvData
    {
        public List<VoucherRow> AllLines { get; set; }
        public Dictionary<string, List<VoucherRow>> DupliciteVouchersDictionary { get; set; }
        public List<VoucherRow> DupliciteLines { get; set; }

        public ReadCsvData(List<VoucherRow> allLines, Dictionary<string, List<VoucherRow>> dupliciteVouchersDictionary, List<VoucherRow> dupliciteLines)
        {
            this.AllLines = allLines;
            this.DupliciteVouchersDictionary = dupliciteVouchersDictionary;
            this.DupliciteLines = dupliciteLines;
        }
    }
}
