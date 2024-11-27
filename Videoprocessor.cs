using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using static System.Math;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Reg;
using Emgu.CV.Structure;
using static FCGR_EmguCV.Videoprocessor;
using System.Windows.Media;
using static System.Net.Mime.MediaTypeNames;
using System.Security.Cryptography;
using Emgu.CV.Cuda;
using DocumentFormat.OpenXml.Spreadsheet;

namespace FCGR_EmguCV
{
    class Videoprocessor
    {

        public string
            videoPath;

        public double
            width, height, a0, b0,
            trans_term_eps = 2e-10,  
            mm_per_pixel,
            mark_start;

        public int
            frame_rate,
            frame_num,
            ind_frame_base,
            serie_duration = 1,
            serie_passduration = 100,

            crop_y1, crop_y2,
            crop_x1, crop_x2,

            sub_height = 100, sub_width = 200, flag = 0,

            trans_numiter = 20;           

        public MotionType
            trans_warp_mode; 

        public List<string>
            savepatch = new List<string>();

        public FrameSet
            FS = new FrameSet();

        public List<VideoCapture>
            Capture = new List<VideoCapture>();

        public LineSegment2D[] lines;

        public System.Drawing.Point p1_crack, p2_crack;

        public List<double> new_d = new List<double>();

        public event EventHandler WarpGraph, LogGraph;

        public class SubSet
        {
            public List<List<Mat>> Frames = new List<List<Mat>>();
            public List<Tuple<double, double>> y = new List<Tuple<double, double>>();
            public List<double> d = new List<double>();
            public List<double> x = new List<double>();
            public List<List<Rectangle>> Frames_roi = new List<List<Rectangle>>();
        }

        public class FrameSet
        {
            public List<List<Mat>> Frames = new List<List<Mat>>();
            public List<List<int>> Frames_ind = new List<List<int>>();
            public Mat Baseframe;
            public SubSet Sub;
            public List<List<SubSet>> Subs = new List<List<SubSet>>();
        }

        // -----------------------------------------------------------------
        // ---------------------------- ФУНКЦИИ ----------------------------
        // -----------------------------------------------------------------

        public void Load(string patch, int ind_bf)
        {
            ind_frame_base = ind_bf;
            videoPath = patch;
            Set_Videopatch(patch);

            VideoCapture cap = new VideoCapture(videoPath);
            width = cap.Get(Emgu.CV.CvEnum.CapProp.FrameWidth);
            height = cap.Get(Emgu.CV.CvEnum.CapProp.FrameHeight);
            frame_rate = (int)cap.Get(Emgu.CV.CvEnum.CapProp.Fps);
            frame_num = (int)cap.Get(Emgu.CV.CvEnum.CapProp.FrameCount);
            Capture.Add(cap);

            Get_BaseFrame(ind_frame_base);
            Get_FramesCollection();
        }

        public void Set_Videopatch(string patch)
        {
            string sp, sp_sf, sp_ar, sp_at;
            patch = patch.Replace(".mp4", "");
            sp = patch + "/images/";
            sp_ar = patch + "/after_rotate+crop/";
            sp_at = patch + "/after_transform/";
            sp_sf = patch + "/subframes/";
            if (!Directory.Exists(sp))
                Directory.CreateDirectory(sp);
            if (!Directory.Exists(sp_ar))
                Directory.CreateDirectory(sp_ar);
            if (!Directory.Exists(sp_at))
                Directory.CreateDirectory(sp_at);
            if (!Directory.Exists(sp_sf))
                Directory.CreateDirectory(sp_sf);
            savepatch.Add(sp);             // = 0
            savepatch.Add(sp_ar);          // = 1
            savepatch.Add(sp_at);          // = 2
            savepatch.Add(sp_sf);          // = 3
        }

        void Crop(Mat frame_in, out Mat frame_out)
        {
            if (crop_x1 < 0 || crop_y1 < 0 || frame_in.Cols - crop_x2 <= 0 || frame_in.Rows - crop_y2 <= 0)
            { Debug.WriteLine("wrong crop!..."); frame_out = frame_in; return; };

            Rectangle Rect = new Rectangle(crop_x1, crop_y1, frame_in.Cols - crop_x2, frame_in.Rows - crop_y2);
            Image<Bgr, Byte> buffer_im = frame_in.ToImage<Bgr, Byte>();
            buffer_im.ROI = Rect;
            frame_out = buffer_im.Copy().Mat;
        }

        void Frame_Rotate(Mat frame_in, out Mat frame_out, double angle)
        {
            Mat frame = frame_in.Clone();
            CvInvoke.CvtColor(frame, frame, Emgu.CV.CvEnum.ColorConversion.Rgb2Gray);
            frame_out = new Mat();
            Mat rotationMatrix = new Mat();
            System.Drawing.Point center = new System.Drawing.Point(frame.Width / 2, frame.Height / 2);
            CvInvoke.GetRotationMatrix2D(center, angle, 1.0, rotationMatrix);
            CvInvoke.WarpAffine(frame, frame_out, rotationMatrix, frame.Size, Inter.Linear, Warp.InverseMap, BorderType.Replicate);
        }

        void Get_BaseFrame(int ind_frame_base)
        {
            Mat frame_curr;
            VideoCapture capture = Capture[0];

            capture.Set(Emgu.CV.CvEnum.CapProp.PosFrames, ind_frame_base);
            frame_curr = capture.QueryFrame();
            if (frame_curr.IsEmpty) { Debug.WriteLine("frame is empty!..."); return; };
            CvInvoke.CvtColor(frame_curr, frame_curr, Emgu.CV.CvEnum.ColorConversion.Bgr2Rgb);
            FS.Baseframe = frame_curr.Clone();
            string saveimg = savepatch[0] + "base.jpg";
            CvInvoke.Imwrite(saveimg, FS.Baseframe);
            Debug.WriteLine("Базовое фото (фрейм " + ind_frame_base.ToString() + ") сохранено: " + saveimg);
        }

        void Get_FramesCollection()
        {
            VideoCapture capture = Capture[0];
            List<int> fr_min = new List<int>();    

            for (int i = ind_frame_base; i <= frame_num; i += serie_passduration * frame_rate)
                fr_min.Add(i);

            for (int i = 0; i < fr_min.Count; i++)
            {
                Debug.WriteLine($"Коллекция фреймов i = {i + 1} из {fr_min.Count}");
                int frame_min = fr_min[i];
                int frame_max = fr_min[i] + serie_duration * frame_rate;
                List<Mat> frames_collection_local = new List<Mat>(); ;
                List<int> ind_local = Enumerable.Range(frame_min, frame_max - frame_min + 1).ToList();
                FS.Frames_ind.Add(ind_local);
                capture.Set(Emgu.CV.CvEnum.CapProp.PosFrames, frame_min);
                for (int j = frame_min; j <= frame_max; j++)
                {
                    Mat frame_curr;
                    frame_curr = capture.QueryFrame();
                    if (frame_curr.IsEmpty) { Debug.WriteLine("frame load: frame is empty!..."); return; };
                    CvInvoke.CvtColor(frame_curr, frame_curr, Emgu.CV.CvEnum.ColorConversion.Bgr2Rgb);
                    frames_collection_local.Add(frame_curr);
                    string saveimg = savepatch[0] + j.ToString() + ".jpg".ToString();
                    CvInvoke.Imwrite(saveimg, frame_curr);
                }
                FS.Frames.Add(frames_collection_local);
            }
        }

        void Subframes_Create(Mat frame, out SubSet sub)
        {
            if (frame.IsEmpty) { Debug.WriteLine("Frame empty!"); sub = new SubSet(); return; }

            sub = new SubSet();
            List<Mat> subframes_local = new List<Mat>();               
            List<Rectangle> subframes_roi_local = new List<Rectangle>();     
            List<Tuple<double, double>> tuples = new List<Tuple<double, double>>();
            List<double> distances = new List<double>();
            List<double> length = new List<double>();

            Mat frame_copy = frame.Clone();

            int x = 0, k = 0;
            string saveimg;
            while (x <= frame.Width)
            {
                k++;
                int y = p1_crack.Y;
                System.Drawing.Point p1 = new System.Drawing.Point(x - sub_width / 2, y + sub_height / 2);
                System.Drawing.Point p2 = new System.Drawing.Point(x - sub_width / 2, y - 3 * sub_height / 2);
                System.Drawing.Size size = new System.Drawing.Size(sub_width, sub_height);
                Tuple<double, double> t = System.Tuple.Create((double)(y + sub_height), (double)(y - sub_height));
                tuples.Add(t);
                length.Add(mark_start + x * mm_per_pixel);
                distances.Add(t.Item1 - t.Item2);
                a0 = t.Item1;
                b0 = t.Item2;
                Rectangle roi1 = new Rectangle(p1, size);
                Rectangle roi2 = new Rectangle(p2, size);
                CvInvoke.Rectangle(frame_copy, roi1, new MCvScalar(255), 3);
                CvInvoke.Rectangle(frame_copy, roi2, new MCvScalar(255), 3);
                saveimg = savepatch[3] + k.ToString() + "down.jpg".ToString();
                Image<Bgr, Byte> buffer_im1 = frame.ToImage<Bgr, Byte>();
                buffer_im1.ROI = roi1;
                Mat subImage1 = buffer_im1.Copy().Mat;
                subframes_local.Add(subImage1);
                CvInvoke.Imwrite(saveimg, subImage1);
                subframes_roi_local.Add(roi1);
                saveimg = savepatch[3] + k.ToString() + "up.jpg".ToString();
                Image<Bgr, Byte> buffer_im2 = frame.ToImage<Bgr, Byte>();
                buffer_im2.ROI = roi2;
                Mat subImage2 = buffer_im2.Copy().Mat;
                subframes_local.Add(subImage2);
                CvInvoke.Imwrite(saveimg, subImage2);
                subframes_roi_local.Add(roi2);   

                sub.Frames.Add(new List<Mat>(subframes_local));
                sub.Frames_roi.Add(new List<Rectangle>(subframes_roi_local));
                subframes_local.Clear();
                subframes_roi_local.Clear();
                x += sub_width / 8;
            }

            sub.y = new List<Tuple<double, double>>(tuples);
            sub.d = new List<double>(distances);

            CvInvoke.Line(frame_copy, p1_crack, p2_crack, new MCvScalar(0, 255, 0), 5, LineType.EightConnected, 0);
            saveimg = savepatch[3] + "subframes.jpg".ToString();
            CvInvoke.Imwrite(saveimg, frame_copy);

            Mat temp = frame_copy.Clone();
            System.Drawing.Size size_temp = new System.Drawing.Size();
            size_temp.Height = temp.Height * 2 / 3;
            size_temp.Width = temp.Width * 2 / 3;
            CvInvoke.Resize(temp, temp, size_temp);
            CvInvoke.Imshow("subrames", temp);
        }

        void Frame_FindLines(Mat input_frame, double eps, int k)
        {
            Mat frame_base = input_frame.Clone();
            Mat base_copy = input_frame.Clone();
            Mat CannyEdges = new Mat();
            List<int> Numbers = new List<int>();
            string saveimg;

            CvInvoke.CvtColor(frame_base, frame_base, Emgu.CV.CvEnum.ColorConversion.Rgb2Gray);
            if (k == 0)
            {
                saveimg = savepatch[k] + "base_gray.jpg";
                CvInvoke.Imwrite(saveimg, frame_base);
            }
            CvInvoke.EqualizeHist(frame_base, frame_base);
            saveimg = savepatch[k] + "base_upgrade.jpg";
            CvInvoke.Imwrite(saveimg, frame_base);
            CvInvoke.ConvertScaleAbs(frame_base, frame_base, 1, 2);
            CvInvoke.Threshold(frame_base, frame_base, 150, 255, ThresholdType.Binary);
            saveimg = savepatch[k] + "base_bin.jpg";
            CvInvoke.Imwrite(saveimg, frame_base);
            CvInvoke.Canny(frame_base, CannyEdges, 150, 255);
            saveimg = savepatch[k] + "base_canny.jpg";
            CvInvoke.Imwrite(saveimg, CannyEdges);
            lines = CvInvoke.HoughLinesP(CannyEdges, 1, Math.PI / 180, 150, 200, 40);

            Debug.WriteLine("Number of lines " + lines.Count().ToString());
            for (int i = 0; i < lines.Count(); i++)
            {
                double xi = lines[i].P2.X - lines[i].P1.X, yi = lines[i].P2.Y - lines[i].P1.Y;
                for (int j = i + 1; j < lines.Count(); j++)
                {
                    double xj = lines[j].P2.X - lines[j].P1.X, yj = lines[j].P2.Y - lines[j].P1.Y;
                    double pr = xi * xj + yi * yj;
                    if (Math.Abs(pr) <= eps)
                    {
                        if (Numbers.IndexOf(i) == -1)
                        {
                            Numbers.Add(i);
                        }
                        if (Numbers.IndexOf(j) == -1)
                        {
                            Numbers.Add(j);
                        }
                    }
                }
            }

            for (int i = 0; i < lines.Count(); i++)
            {
                CvInvoke.Line(CannyEdges, lines[i].P1, lines[i].P2, new MCvScalar(255), 5, LineType.EightConnected, 0);
                if (Numbers.IndexOf(i) != -1)
                {
                    CvInvoke.Line(base_copy, lines[i].P1, lines[i].P2, new MCvScalar(255), 5, LineType.EightConnected, 0);
                }
            }

            saveimg = savepatch[k] + "base_canny_lines.jpg";
            CvInvoke.Imwrite(saveimg, CannyEdges);

            for (int i = 0; i < lines.Count(); i++)
            {

                if (Numbers.IndexOf(i) != -1)
                {
                    System.Drawing.Point p = new System.Drawing.Point();
                    p.X = (lines[i].P1.X + lines[i].P2.X) / 2;
                    p.Y = (lines[i].P1.Y + lines[i].P2.Y) / 2;
                    CvInvoke.PutText(base_copy, (i + 1).ToString(), p, FontFace.HersheyPlain, 2, new MCvScalar(0, 0, 255), 2);
                }
            }

            saveimg = savepatch[k] + "base_lines.jpg";
            CvInvoke.Imwrite(saveimg, base_copy);

            Mat frame = base_copy.Clone();

            System.Drawing.Size size = new System.Drawing.Size();
            size.Height = frame.Height * 2 / 3;
            size.Width = frame.Width * 2 / 3;
            CvInvoke.Resize(frame, frame, size);
            CvInvoke.Imshow("lines", frame);
        }

        void Frame_Transform(Mat frame_base, Mat frame_in, Mat trans_frame, out double[,]? WarpMatrix)
        {
            if (frame_base.IsEmpty || frame_in.IsEmpty) { Debug.WriteLine("frames empty!"); WarpMatrix = null; return; }

            Mat frame0,
                framei,
                frame0_gray = new Mat(),
                framei_gray = new Mat(),
                warp_matrix = new Mat();

            frame0 = frame_base.Clone();
            framei = frame_in.Clone();

            if (frame0.NumberOfChannels > 1)
                CvInvoke.CvtColor(frame0, frame0_gray, Emgu.CV.CvEnum.ColorConversion.Rgb2Gray);
            else
                frame0.CopyTo(frame0_gray);

            if (framei.NumberOfChannels > 1)
                CvInvoke.CvtColor(framei, framei_gray, Emgu.CV.CvEnum.ColorConversion.Rgb2Gray);
            else
                framei.CopyTo(framei_gray);


            if (trans_warp_mode == MotionType.Homography)
                warp_matrix = Mat.Eye(3, 3, DepthType.Cv32F, 1);
            else
                warp_matrix = Mat.Eye(2, 3, DepthType.Cv32F, 1);

            MCvTermCriteria criteria = new MCvTermCriteria(trans_numiter, trans_term_eps);

            CvInvoke.FindTransformECC(frame0_gray,
                                      framei_gray,
                                      warp_matrix,
                                      trans_warp_mode,
                                      criteria);

            if (trans_warp_mode != MotionType.Homography)
                CvInvoke.WarpAffine
                    (framei, trans_frame, warp_matrix, frame0.Size, Inter.Linear, Warp.InverseMap, BorderType.Replicate);
            else
                CvInvoke.WarpPerspective
                    (framei, trans_frame, warp_matrix, frame0.Size, Inter.Linear, Warp.InverseMap, BorderType.Replicate);

            if (trans_frame.IsEmpty)
            {
                Debug.WriteLine("trans_frame empty!");
                WarpMatrix = null;
                return;
            }
            else
            {
                int n;
                if (trans_warp_mode == MotionType.Homography)
                    n = 3;
                else n = 2;
                Image<Gray, float> matrix = warp_matrix.ToImage<Gray, float>();
                WarpMatrix = new double[n, 3];
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < 3; j++)
                        WarpMatrix[i, j] = matrix[i, j].Intensity;
            }
        }

        public void Method_FindLines(double eps, int k)
        {
            if (FS.Baseframe.IsEmpty) { Debug.WriteLine("Base frame empty!"); return; }
            Frame_FindLines(FS.Baseframe, eps, k);
        }

        public void Method_Rotate(int k)
        {
            if (FS.Baseframe.IsEmpty) { Debug.WriteLine("Base frame empty!"); return; }

            System.Drawing.Point p1_risk = lines[k].P1;
            System.Drawing.Point p2_risk = lines[k].P2;

            double angle_risk = Math.Atan2(lines[k].P2.Y - lines[k].P1.Y, lines[k].P2.X - lines[k].P1.X) * 180.0 / Math.PI;
            Debug.WriteLine("Angle of risk = " + angle_risk);

            crop_x1 = 0;
            crop_x2 = 300;
            crop_y1 = 0;
            crop_y2 = 200;

            string saveimg = savepatch[1] + "base_rotate.jpg";
            Mat frame_base;
            Frame_Rotate(FS.Baseframe, out frame_base, 90 - angle_risk);
            Crop(frame_base, out frame_base);
            FS.Baseframe = frame_base.Clone();
            CvInvoke.Imwrite(saveimg, FS.Baseframe);

            Mat frame = frame_base.Clone();
            System.Drawing.Size size = new System.Drawing.Size();
            size.Height = frame.Height * 2 / 3;
            size.Width = frame.Width * 2 / 3;
            CvInvoke.Resize(frame, frame, size);
            CvInvoke.Imshow("after_rotate", frame);

            int num;
            for (int i = 0; i < FS.Frames.Count; i++)
            {
                num = i * serie_passduration * frame_rate;
                for (int j = 0; j < FS.Frames[i].Count; j++)
                {
                    Mat curr;
                    saveimg = savepatch[1] + (num + j).ToString() + "_rotate.jpg";
                    Frame_Rotate(FS.Frames[i][j], out curr, 90 - angle_risk);
                    Crop(curr, out curr);
                    FS.Frames[i][j] = curr.Clone();
                    CvInvoke.Imwrite(saveimg, FS.Frames[i][j]);
                }
            }
        }

        public void Method_InputCrack(int k)
        {
            if (FS.Baseframe.IsEmpty) { Debug.WriteLine("Base frame empty!"); return; }

            p1_crack = new System.Drawing.Point();
            p1_crack.X = 0;
            p1_crack.Y = (lines[k].P1.Y + lines[k].P2.Y) / 2;
            p2_crack = new System.Drawing.Point();
            p2_crack.X = FS.Baseframe.Width;
            p2_crack.Y = (lines[k].P1.Y + lines[k].P2.Y) / 2;

            Mat frame = FS.Baseframe.Clone();
            CvInvoke.Line(frame, p1_crack, p2_crack, new MCvScalar(255), 5, LineType.EightConnected, 0);
            System.Drawing.Size size = new System.Drawing.Size();
            size.Height = frame.Height * 2 / 3;
            size.Width = frame.Width * 2 / 3;
            CvInvoke.Resize(frame, frame, size);
            CvInvoke.Imshow("crack_line", frame);
        }

        public void Method_FindMPP(int i, int j, double m1, double m2)
        {
            System.Drawing.Point p1_risk1 = lines[i].P1;
            System.Drawing.Point p2_risk1 = lines[i].P2;
            System.Drawing.Point p1_risk2 = lines[j].P1;
            System.Drawing.Point p2_risk2 = lines[j].P2;

            double x1 = (p1_risk1.X + p2_risk1.X) / 2;
            double x2 = (p1_risk2.X + p2_risk2.X) / 2;

            p1_risk1.X = (p1_risk1.X + p2_risk1.X) / 2;
            p1_risk1.Y = 0;
            p2_risk1.X = (p1_risk1.X + p2_risk1.X) / 2;
            p2_risk1.Y = FS.Baseframe.Height;

            p1_risk2.X = (p1_risk2.X + p2_risk2.X) / 2;
            p1_risk2.Y = 0;
            p2_risk2.X = (p1_risk2.X + p2_risk2.X) / 2;
            p2_risk2.Y = FS.Baseframe.Height;
            

            mm_per_pixel = Math.Abs(m1 - m2) / (Math.Abs((int)x1 - (int)x2));
            Debug.WriteLine("In one pixel on the axis X there is " + mm_per_pixel.ToString() + " mm");

            mark_start = m1 - mm_per_pixel * x1;
            Debug.WriteLine("begin: " + mark_start.ToString() + " mm");

            Mat frame = FS.Baseframe.Clone();
            CvInvoke.Line(frame, p1_risk1, p2_risk1, new MCvScalar(255), 5, LineType.EightConnected, 0);
            CvInvoke.Line(frame, p1_risk2, p2_risk2, new MCvScalar(255), 5, LineType.EightConnected, 0);
            string saveimg = savepatch[1] + "risks.jpg";
            CvInvoke.Imwrite(saveimg, frame);

            System.Drawing.Size size = new System.Drawing.Size();
            size.Height = frame.Height * 2 / 3;
            size.Width = frame.Width * 2 / 3;
            CvInvoke.Resize(frame, frame, size);
            CvInvoke.Imshow("risks", frame); 
        }

        void Filter_MovingAverage(List<double> curr, out List<double> new_curr, int windowSize) 
        {
            double alpha = 1 / (double)windowSize;
            new_curr = new List<double>();

            for (int i = 0; i < windowSize - 1; ++i)
                new_curr.Add(curr[i]);

            for (int i = windowSize - 1; i < curr.Count; ++i)
            {
                double sum = 0;
                for (int j = 0; j < windowSize; ++j)
                    sum += curr[i - j] * alpha;
                new_curr.Add(sum);
            }
        }

        void Calc_Graph(SubSet curr)
        {
            List<double> list = new List<double>();
            List<double> new_list = new List<double>();
            if (flag == 0)
            {
                flag = 1;
                for (int i = 0; i < curr.y.Count; ++i)
                    list.Add(curr.y[i].Item2);
                /*Filter_MovingAverage(list, out new_list, 4);
                for (int i = 0; i < new_list.Count; ++i)
                    new_d.Add(new_list[i]);*/
                for (int i = 0; i < list.Count; ++i)
                    new_d.Add(list[i]);
            }
            else if (flag == 1)
            {
                flag = 2;
                for (int i = 0; i < curr.y.Count; ++i)
                    list.Add(curr.y[i].Item1);
                /*Filter_MovingAverage(list, out new_list, 4);
                for (int i = 0; i < new_list.Count; ++i)
                    new_d[i] = Math.Abs(new_d[i] - new_list[i]);*/
                for (int i = 0; i < list.Count; ++i)
                    new_d[i] = Math.Abs(new_d[i] - list[i]);
            }

            /*WarpGraph?.Invoke(new_list, EventArgs.Empty);
            if (flag == 2)
                WarpGraph?.Invoke(new_d, EventArgs.Empty);*/
            WarpGraph?.Invoke(list, EventArgs.Empty);
            if (flag == 2)
                WarpGraph?.Invoke(new_d, EventArgs.Empty);
        }

        List<double> Calc_Param(List<double> curr)
        {
            double alpha = 0.01, L = 200, k = -10, x0 = 10;
            double sum_error = 20;
            while (Math.Abs(sum_error) >= 20) {
                sum_error = 0;
                double L_gradient = 0, k_gradient = 0, x0_gradient = 0;
                for (int i = 0; i < curr.Count; i++)
                {
                    double y_pred = L / (1 + Math.Exp(-k * (i - x0)));
                    double error = y_pred - curr[i];
                    sum_error += error;
                    L_gradient += 2 * error / (1 + Math.Exp(k * (i - x0)));
                    k_gradient += 2 * error * L * (i - x0) * Math.Exp(k * (i - x0)) / Math.Pow(1 + Math.Exp(k * (i - x0)), 2);
                    x0_gradient += 2 * error * L * k * Math.Exp(k * (i - x0)) / Math.Pow(1 + Math.Exp(k * (i - x0)), 2);
                }
                if (Math.Abs(sum_error) >= 0.1)
                {
                    L -= alpha * L_gradient;
                    k -= alpha * k_gradient;
                    x0 -= alpha * x0_gradient;
                }
            }
            List<double> temp = new List<double>();
            temp.Add(L);
            temp.Add(k);
            temp.Add(x0);
            return temp;
        }

        public void Method_CalcCrack()
        {
            if (FS.Frames.Count == 0) { Debug.WriteLine("Frames collection empty!"); return; }
            if (FS.Baseframe.IsEmpty) { Debug.WriteLine("Base frame empty!"); return; }

            trans_warp_mode = MotionType.Homography;
            int num, opt_i = 0, opt_j = 0;
            for (int i = 0; i < FS.Frames.Count; i++)
            {
                num = i * serie_passduration * frame_rate;
                double min = 24;
                for (int j = 0; j < FS.Frames[i].Count; j++)
                {
                    double[,] WarpMatrix;
                    Frame_Transform(FS.Baseframe, FS.Frames[i][j], FS.Frames[i][j], out WarpMatrix);
                    double y_loc = WarpMatrix[0, 2];
                    Debug.WriteLine((num  + j).ToString() + " frame y_loc = " + y_loc.ToString());
                    if (Math.Abs(y_loc) < min && num + j != 0)
                    {
                        min = Math.Abs(y_loc);
                        opt_i = i;
                        opt_j = j;
                    }
                    string saveimg = savepatch[2] + (num + j).ToString() + "_transform.jpg";
                    CvInvoke.Imwrite(saveimg, FS.Frames[i][j]);
                }
            }
            Debug.WriteLine("optimal frame number = " + (opt_i * serie_passduration * frame_rate + opt_j).ToString());

            Subframes_Create(FS.Baseframe, out FS.Sub);

            trans_warp_mode = MotionType.Homography;
            for (int i = 0; i < FS.Frames.Count; i++)
            {
                num = i * serie_passduration * frame_rate;
                List<SubSet> List_Sub_temp = new List<SubSet>();

                for (int j = 0; j < FS.Frames[i].Count; j++)
                {
                    SubSet Sub_temp = new SubSet();
                    Mat frame_curr = FS.Frames[i][j];

                    for (int k = 0; k < FS.Sub.Frames_roi.Count; k++)
                    {
                        double a = 0, b = 0;
                        for (int m = 0; m < 2; m++)
                        {
                            double[,] WarpMatrix_local;
                            Rectangle roi_curr = FS.Sub.Frames_roi[k][m];
                            Image<Gray, Byte> buffer_im = frame_curr.ToImage<Gray, Byte>();
                            buffer_im.ROI = roi_curr;
                            Mat subframe_curr = buffer_im.Copy().Mat;
                            Frame_Transform(FS.Sub.Frames[k][m], subframe_curr, subframe_curr, out WarpMatrix_local);
                            double y_loc = WarpMatrix_local[0, 2];
                            string s;
                            if (m == 0)
                            {
                                a = y_loc + a0;
                                s = "up";
                            }
                            else
                            {
                                b = y_loc + b0;
                                s = "down";
                            }
                            Debug.WriteLine((num + j).ToString() + " frame " + (k + 1).ToString() + " pair " + s + " y_loc = " + y_loc.ToString());
                        }

                        Tuple<double, double> temp = System.Tuple.Create(a, b);
                        Sub_temp.y.Add(temp);
                        Sub_temp.d.Add(a - b);
                        Debug.WriteLine((num + j).ToString() + " frame " + (k + 1).ToString() + " pair d = " + (a - b).ToString());
                    }

                    List_Sub_temp.Add(Sub_temp);
                }
                FS.Subs.Add(List_Sub_temp);
            }
            Calc_Graph(FS.Subs[opt_i][opt_j]);
            Calc_Graph(FS.Subs[opt_i][opt_j]);
            Debug.WriteLine(a0.ToString() + " " + b0.ToString());

            /*List<double> param = Calc_Param(new_d);
            Debug.WriteLine(param[0].ToString() + " " + param[1].ToString() + " " + param[2].ToString());
            for (int i = 0; i < new_d.Count; i++)
                new_d[i] = param[0] / (1 + Math.Exp(-param[1] * (i - param[2])));
            LogGraph?.Invoke(new_d, EventArgs.Empty);*/
        }
    }
}
