using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot;
using OxyPlot.Wpf;
using System.Collections.Generic;
using System.Diagnostics;
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
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Drawing.Diagrams;
using OxyPlot.Legends;

namespace FCGR_EmguCV
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 230614153707119
    /// 230614154114819
    public partial class MainWindow : Window
    {
        Videoprocessor Video = new Videoprocessor();

        string dir_file = @"C:\Users\DEN\Desktop\FCGR-EmguCV\Video\230614154114819.mp4";

        int ind_base = 0, flag = 0;

        bool ind = false;

        XLWorkbook xlwb = new XLWorkbook();

        XLWorkbook xlwb_test = new XLWorkbook(@"C:\Users\DEN\Desktop\FCGR-EmguCV\Results\test2.xlsx");

        List<double> frontSide, backSide, frontCycles, backCycles;

        List<double> v_front = new List<double>();
        List<double> v_back = new List<double>();
        List<double> k_front = new List<double>();
        List<double> k_back = new List<double>();
        List<double> v_front_lg = new List<double>();
        List<double> v_back_lg = new List<double>();
        List<double> k_front_lg = new List<double>();
        List<double> k_back_lg = new List<double>();
        List<double> v_front_mnk = new List<double>();
        List<double> v_back_mnk = new List<double>();
        List<double> v_front_kdff = new List<double>();
        List<double> v_back_kdff = new List<double>();

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;
            Video.WarpGraph += Video_WarpShow;
            Video.LogGraph += Video_LogShow;
        }

        private PlotModel _plotMod;
        public PlotModel PlotMod
        {
            get { return _plotMod; }
            set { _plotMod = value; }
        }

        private void SetupPlotMod(PlotModel PlotMod_loc, List<DataPoint> Points_loc)
        {
            var pointLine = new LineSeries
            {
                Color = OxyColor.FromRgb(0, 0, 255),
                MarkerSize = 4,
                MarkerType = OxyPlot.MarkerType.Square,
                MarkerStroke = OxyColor.FromRgb(0, 0, 0),
                MarkerFill = OxyColor.FromRgb(255, 255, 0)
            };

            foreach (var dataPoint in Points_loc)
            {
                pointLine.Points.Add(dataPoint);
            }
            PlotMod_loc.Series.Add(pointLine);
        }

        private void SetupPlotMod_test(PlotModel PlotMod_loc, List<double> frontX, List<double> frontY, List<double> backX, List<double> backY)
        {
            var leftLegend = new Legend
            {
                LegendPlacement = LegendPlacement.Inside,
                LegendPosition = LegendPosition.LeftTop,
                LegendBackground = OxyColor.FromAColor(200, OxyColors.White),
                LegendBorder = OxyColors.Black,
                Key = "Legend Left",
            };

            PlotMod_loc.Legends.Add(leftLegend);

            var pointLine1 = new LineSeries
            {
                LegendKey = leftLegend.Key,
                Color = OxyColor.FromRgb(0, 0, 255),
                MarkerSize = 2,
                Title = "Front side",
                MarkerType = OxyPlot.MarkerType.Square,
                MarkerStroke = OxyColor.FromRgb(0, 0, 0),
                MarkerFill = OxyColor.FromRgb(255, 255, 0)
            };

            var pointLine2 = new LineSeries
            {
                LegendKey = leftLegend.Key,
                Color = OxyColor.FromRgb(0, 255, 0),
                MarkerSize = 2,
                Title = "Back side",
                MarkerType = OxyPlot.MarkerType.Square,
                MarkerStroke = OxyColor.FromRgb(0, 0, 0),
                MarkerFill = OxyColor.FromRgb(255, 0, 255)
            };

            List<DataPoint> DataPoints1 = new List<DataPoint>();
            List<DataPoint> DataPoints2 = new List<DataPoint>();

            for (int i = 0; i < frontX.Count(); i++)
            {
                DataPoints1.Add(new DataPoint(frontX[i], frontY[i]));
            }
            for (int i = 0; i < backX.Count(); i++)
            {
                DataPoints2.Add(new DataPoint(backX[i], backY[i]));
            }

            foreach (var dataPoint in DataPoints1)
            {
                pointLine1.Points.Add(dataPoint);
            }
            foreach (var dataPoint in DataPoints2)
            {
                pointLine2.Points.Add(dataPoint);
            }

            PlotMod_loc.Series.Add(pointLine1);
            PlotMod_loc.Series.Add(pointLine2);
        }

        private void CreatePlotMod(PlotModel PlotMod)
        {
            PlotMod.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "i" });
            if (flag == 0 || flag == 1)
                PlotMod.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "y, pix" });
            else
                PlotMod.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "d, pix" });
        }

        private void CreatePlotMod_test(PlotModel PlotMod)
        {
            if (flag == 0)
            {
                PlotMod.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "N, cycles" });
                PlotMod.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "l, mm" });
            }
            else if (flag == 1)
            {
                PlotMod.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "ΔK" });
                PlotMod.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "dl/dN, mm/cycle" });
            }
            else if (flag == 2)
            {
                PlotMod.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "lg(ΔK)" });
                PlotMod.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "lg(dl/dN)" });
            }
        }

        private void write_numbers(List<double> curr, int n, string s)
        {
            var worksheet = xlwb.Worksheets.First();
            worksheet.Cell(1, n).SetValue(s);
            for (int i = 1; i < curr.Count + 1; i++)
            {
                worksheet.Cell(i + 1, n).SetValue(curr[i - 1]);
            }
            xlwb.SaveAs("data.xlsx");
        }

        private void Video_WarpShow(object sender, EventArgs e)
        {
            List<double> Y = (List<double>)sender;

            string s_title;
            if (flag == 0)
                s_title = "top row";
            else if (flag == 1)
                s_title = "bottom row";
            else
                s_title = "distance";
            PlotModel PlotMod_loc = new PlotModel();
            PlotMod_loc.Title = s_title;
            CreatePlotMod(PlotMod_loc);
            List<DataPoint> DataPoints = new List<DataPoint>();

            for (int i = 0; i < Y.Count(); i++)
            {
                DataPoints.Add(new DataPoint(i, Y[i]));   
            }

            SetupPlotMod(PlotMod_loc, DataPoints);
            if (flag == 0)
            {
                UIPlot1.Model = PlotMod_loc;
                flag = 1;
                var controller = new PlotController();
                controller.BindMouseDown(OxyMouseButton.Left, OxyPlot.PlotCommands.PanAt);
                controller.BindMouseDown(OxyMouseButton.Right, OxyPlot.PlotCommands.ZoomRectangle);
                controller.BindMouseWheel(OxyModifierKeys.None, OxyPlot.PlotCommands.ZoomWheel);
                UIPlot1.Controller = controller;
                xlwb.AddWorksheet();
                write_numbers(Y, 1, "y top row");
            }
            else if (flag == 1)
            {
                UIPlot2.Model = PlotMod_loc;
                flag = 2;
                var controller = new PlotController();
                controller.BindMouseDown(OxyMouseButton.Left, OxyPlot.PlotCommands.PanAt);
                controller.BindMouseDown(OxyMouseButton.Right, OxyPlot.PlotCommands.ZoomRectangle);
                controller.BindMouseWheel(OxyModifierKeys.None, OxyPlot.PlotCommands.ZoomWheel);
                UIPlot2.Controller = controller;
                write_numbers(Y, 2, "y bottom row");
            }
            else
            {
                UIPlot3.Model = PlotMod_loc;
                var controller = new PlotController();
                controller.BindMouseDown(OxyMouseButton.Left, OxyPlot.PlotCommands.PanAt);
                controller.BindMouseDown(OxyMouseButton.Right, OxyPlot.PlotCommands.ZoomRectangle);
                controller.BindMouseWheel(OxyModifierKeys.None, OxyPlot.PlotCommands.ZoomWheel);
                UIPlot3.Controller = controller;
                write_numbers(Y, 3, "d");
                flag = 0;
            }
        }

        private void Video_LogShow(object sender, EventArgs e)
        {
            List<double> Y = (List<double>)sender;
            PlotModel PlotMod_loc = new PlotModel();
            PlotMod_loc.Title = "distance";
            CreatePlotMod(PlotMod_loc);
            List<DataPoint> DataPoints = new List<DataPoint>();

            for (int i = 0; i < Y.Count(); i++)
            {
                DataPoints.Add(new DataPoint(i, Y[i]));
            }

            UIPlot4.Model = PlotMod_loc;
            var controller = new PlotController();
            controller.BindMouseDown(OxyMouseButton.Left, OxyPlot.PlotCommands.PanAt);
            controller.BindMouseDown(OxyMouseButton.Right, OxyPlot.PlotCommands.ZoomRectangle);
            controller.BindMouseWheel(OxyModifierKeys.None, OxyPlot.PlotCommands.ZoomWheel);
            UIPlot4.Controller = controller;
        }

        private void BLoad(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Загружаем видеофайл...");
            Video.Load(dir_file, ind_base);
            Debug.WriteLine("ок");
        }

        private void BFind(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Ищем линии...");
            if (ind)
            {
                double eps = double.Parse(Microsoft.VisualBasic.Interaction.InputBox("Введите точность: "));
                Video.Method_FindLines(eps, 1);
                Debug.WriteLine("ок");
            }
            else
            {
                double eps = double.Parse(Microsoft.VisualBasic.Interaction.InputBox("Введите точность: "));
                Video.Method_FindLines(eps, 0);
                Debug.WriteLine("ок");
            }
        }

        private void BRotate(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Ждём номер риски...");
            int result = int.Parse(Microsoft.VisualBasic.Interaction.InputBox("Введите номер риски:"));
            Video.Method_Rotate(result - 1);
            ind = true;
            Debug.WriteLine("ок");
        }

        private void BInput(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Ждём номер трещины...");
            int result = int.Parse(Microsoft.VisualBasic.Interaction.InputBox("Введите номер трещины:"));
            Video.Method_InputCrack(result - 1);
            int risk1 = int.Parse(Microsoft.VisualBasic.Interaction.InputBox("Введите номер линии первой риски:"));
            double mark1 = double.Parse(Microsoft.VisualBasic.Interaction.InputBox("Введите значение первой риски (в мм):"));
            int risk2 = int.Parse(Microsoft.VisualBasic.Interaction.InputBox("Введите номер линии второй риски:"));
            double mark2 = double.Parse(Microsoft.VisualBasic.Interaction.InputBox("Введите значение второй риски (в мм):"));
            Video.Method_FindMPP(risk1 - 1, risk2 - 1, mark1, mark2);
            Debug.WriteLine("ок");
        }

        private void BCalc(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Проводим расчёт трещины...");
            Video.Method_CalcCrack();
            Debug.WriteLine("ок");
        }

        private void inputFromFile(out List<double> curr, int n, int m, int k)
        {
            var worksheet = xlwb_test.Worksheets.First();
            List<double> list = new List<double>();
            for (int i = n; i <= m; ++i)
            {
                var cellValue = worksheet.Cell(i, k).Value;
                list.Add(cellValue.GetNumber());
            }
            curr = new List<double>(list);
        }

        private void paramMNK(List<double> x, List<double> y, out double a, out double b)
        {
            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            int n = x.Count;

            for (int i = 0; i < n; ++i)
            {
                sumX += x[i];
                sumY += y[i];
                sumXY += x[i] * y[i];
                sumX2 += x[i] * x[i];
            }

            a = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
            b = (sumY - a * sumX) / n;
        }

        private void BGraph(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Проводим расчёт КДУР...");
            inputFromFile(out frontCycles, 34, 89, 2);
            inputFromFile(out frontSide, 34, 89, 3);
            inputFromFile(out backCycles, 34, 89, 5);
            inputFromFile(out backSide, 34, 89, 6);
            PlotModel PlotMod_loc = new PlotModel();
            PlotMod_loc.Title = "results";
            CreatePlotMod_test(PlotMod_loc);
            SetupPlotMod_test(PlotMod_loc, frontCycles, frontSide, backCycles, backSide);
            UIPlot1.Model = PlotMod_loc;
            var controller = new PlotController();
            controller.BindMouseDown(OxyMouseButton.Left, OxyPlot.PlotCommands.PanAt);
            controller.BindMouseDown(OxyMouseButton.Right, OxyPlot.PlotCommands.ZoomRectangle);
            controller.BindMouseWheel(OxyModifierKeys.None, OxyPlot.PlotCommands.ZoomWheel);
            UIPlot1.Controller = controller;

            double t = 6.19, b = 60, delta_p = 495;

            for (int i = 0; i < frontCycles.Count - 1; ++i)
            {
                double alpha = frontSide[i] / b;
                v_front.Add((frontSide[i + 1] - frontSide[i]) / ((frontCycles[i + 1] - frontCycles[i])));
                double delta_k = delta_p / (t * Math.Sqrt(b)) * (2 + alpha) / Math.Pow(1 - alpha, 3 / 2) *
                    (0.886 + 4.64 * alpha - 13.32 * Math.Pow(alpha, 2) + 14.72 * Math.Pow(alpha, 3) - 5.6 * Math.Pow(alpha, 4));
                k_front.Add(delta_k);
                v_front_lg.Add(Math.Log10(v_front[i]));
                k_front_lg.Add(Math.Log10(k_front[i]));
            }
            for (int i = 0; i < backCycles.Count - 1; ++i)
            {
                double alpha = backSide[i] / b;
                v_back.Add((backSide[i + 1] - backSide[i]) / ((backCycles[i + 1] - backCycles[i])));
                double delta_k = delta_p / (t * Math.Sqrt(b)) * (2 + alpha) / Math.Pow(1 - alpha, 3 / 2) *
                    (0.886 + 4.64 * alpha - 13.32 * Math.Pow(alpha, 2) + 14.72 * Math.Pow(alpha, 3) - 5.6 * Math.Pow(alpha, 4));
                k_back.Add(delta_k);
                v_back_lg.Add(Math.Log10(v_back[i]));
                k_back_lg.Add(Math.Log10(k_back[i]));
            }

            double a_f, b_f, a_b, b_b;

            paramMNK(k_front_lg, v_front_lg, out a_f, out b_f);
            paramMNK(k_back_lg, v_back_lg, out a_b, out b_b);

            for (int i = 0; i < k_front_lg.Count; ++i)
                v_front_mnk.Add(a_f * k_front_lg[i] + b_f);
            for (int i = 0; i < k_back_lg.Count; ++i)
                v_back_mnk.Add(a_b * k_back_lg[i] + b_b);

            for (int i = 0; i < k_front.Count; ++i)
                v_front_kdff.Add(Math.Pow(10, v_front_mnk[i]));
            for (int i = 0; i < k_back.Count; ++i)
                v_back_kdff.Add(Math.Pow(10, v_back_mnk[i]));

            flag = 1;

            PlotModel PlotMod_speed = new PlotModel();
            PlotMod_speed.Title = "speed";
            CreatePlotMod_test(PlotMod_speed);
            SetupPlotMod_test(PlotMod_speed, k_front, v_front, k_back, v_back);
            UIPlot2.Model = PlotMod_speed;
            UIPlot2.Controller = controller;

            flag = 2;

            PlotModel PlotMod_lg = new PlotModel();
            PlotMod_lg.Title = "lg";
            CreatePlotMod_test(PlotMod_lg);
            SetupPlotMod_test(PlotMod_lg, k_front_lg, v_front_lg, k_back_lg, v_back_lg);
            UIPlot3.Model = PlotMod_lg;
            UIPlot3.Controller = controller;

            PlotModel PlotMod_mnk = new PlotModel();
            PlotMod_mnk.Title = "mnk";
            CreatePlotMod_test(PlotMod_mnk);
            SetupPlotMod_test(PlotMod_mnk, k_front_lg, v_front_mnk, k_back_lg, v_back_mnk);
            UIPlot4.Model = PlotMod_mnk;
            UIPlot4.Controller = controller;

            flag = 1;

            PlotModel PlotMod_kdff = new PlotModel();
            PlotMod_kdff.Title = "KDFF";
            CreatePlotMod_test(PlotMod_kdff);
            SetupPlotMod_test(PlotMod_kdff, k_front, v_front_kdff, k_back, v_back_kdff);
            UIPlot5.Model = PlotMod_kdff;
            UIPlot5.Controller = controller;

            Debug.WriteLine("ок");
        }
    }
}