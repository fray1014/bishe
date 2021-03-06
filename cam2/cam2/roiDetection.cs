﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;
using Emgu.Util;
using Newtonsoft.Json;
using System.IO;

namespace cam2
{
    public partial class roiDetection : Form
    {
        private static Capture _cameraCapture;
        private Image<Bgr, Byte> frame ;
        private Image<Gray, Byte>[] canny_frame = new Image<Gray,Byte>[3];
        private Image<Gray, Byte> canny_out ;
        static int cnt = 0;//消抖计数器
        static int th1 = 5;//canny第一阈值初始化
        static int th2 = 70;//canny第二阈值初始化
        private int size_of_slide_max = 80 * 200;//玻片大小上界
        private int size_of_slide_min = 40*160;//玻片大小下界
        private int size_of_roi_max = 200;//染色区域大小上界
        private int size_of_roi_min = 100;//染色区域大小下界
        private MCvBox2D tempbox = new MCvBox2D();//用于标注玻片位置
        private static List<Rectangle> s1Roi = new List<Rectangle>();
        private static List<Rectangle> s2Roi = new List<Rectangle>();
        private static List<Rectangle> s3Roi = new List<Rectangle>();
        private static List<Rectangle> s4Roi = new List<Rectangle>();
        private static List<Rectangle>[] roi = { s1Roi, s2Roi, s3Roi, s4Roi };//染色区域
        private static Rectangle[] slide = new Rectangle[4];//玻片
        private Image<Bgr, Byte>[] slide_img = new Image<Bgr, Byte>[4];
        private Image<Gray, Byte>[] slide_gray_img = new Image<Gray, Byte>[4];
        private static bool isSlide = false;//玻片检测标志
        private static bool scanDone = false;//扫描结束标志位
        private static bool ampChoose = false;
        private static bool filedirChoose = false;
        private static bool startFlag = false;
        private string fileDir;
        private static int amp = 1;//物镜倍数
        private static int slideNum = 5;//玻片扫描数
        private static int slideIndex = 0;
        private static int roiNum = 0;//ROI个数
        public static Rectangle rect1stBLK = new Rectangle(125, 115, 150, 30);
        public static Rectangle rect2ndBLK = new Rectangle(525, 115, 150, 30);
        public static Rectangle rect3rdBLK = new Rectangle(925, 115, 150, 30);
        public static Rectangle rect4thBLK = new Rectangle(1325, 115, 150, 30);
        public static Rectangle[] rectsBLK = { rect1stBLK, rect2ndBLK, rect3rdBLK , rect4thBLK };
        public roiDetection()
        {
            InitializeComponent();
            Run();
        }
        //Backgroundworker多线程
        //System.thread多线程
        //visual assistant X查看代码工具
        void Run()
        {
            try
            {
                //参数0为默认摄像头，后续为外接摄像头
                _cameraCapture = new Capture(1);

                canThBar1.Maximum = 255;
                canThBar1.Minimum = 0;
                canThBar1.SmallChange = 2;

                canThBar2.Maximum = 255;
                canThBar2.Minimum = 0;
                canThBar2.SmallChange = 2;

                slideMinSizeBar.Maximum = 100 * 100;
                slideMinSizeBar.Minimum = 1000;
                slideMinSizeBar.SmallChange = 500;
                slideMaxSizeBar.Maximum = 150 * 250;
                slideMaxSizeBar.Minimum = 1000;
                slideMaxSizeBar.SmallChange = 500;

                roiMinSizeBar.Maximum = 50 * 50;
                roiMinSizeBar.Minimum = 1;
                roiMinSizeBar.SmallChange = 10;
                roiMaxSizeBar.Maximum = 50 * 50;
                roiMaxSizeBar.Minimum = 1;
                roiMaxSizeBar.SmallChange = 10;

                canThBar1.Value = th1;
                canThBar2.Value = th2;
                slideMinSizeBar.Value = size_of_slide_min;
                slideMaxSizeBar.Value = size_of_slide_max;
                roiMinSizeBar.Value = size_of_roi_min;
                roiMaxSizeBar.Value = size_of_roi_max;

                cannyBox1.Text = Convert.ToString(th1);
                cannyBox2.Text = Convert.ToString(th2);
                slideMinSize.Text = Convert.ToString(size_of_slide_min);
                slideMaxSize.Text = Convert.ToString(size_of_slide_max);
                roiMinSize.Text = Convert.ToString(size_of_roi_min);
                roiMaxSize.Text = Convert.ToString(size_of_roi_max);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
                return;
            }
            Application.Idle += ProcessFrame;
        }

        void ProcessFrame(object sender, EventArgs e)
        {
            frame = _cameraCapture.QueryFrame();
            canny_out = frame.Convert<Gray, Byte>();
            //frame._SmoothGaussian(3); //filter out noises
            if(startFlag)
            {
                
                for (int i = 0; i < slideNum; i++)
                    roi[i].Clear();
                SlideDetection();
                if(isSlide)
                {
                    for (int i = 0; i < slideNum; i++)
                        RegionDetection(i);
                }
                
            }
            else
            {
                Stable_Frame();
                
            }
        }
        /*动态消抖*/
        void Stable_Frame()//传参Image<Gray, Byte> image
        {
            frame = _cameraCapture.QueryFrame();
            imageBox1.Image = frame;
            if (cnt < 2)
            {
                canny_frame[cnt] = frame.Canny(th1, th2);
                cnt++;
            }
            else
            {
                int[,] eq_flag = new int[imageBox1.Height, imageBox1.Width];
                canny_frame[cnt] = frame.Canny(th1, th2);
                for (int i = 0; i < imageBox1.Height; i++)
                {
                    for (int j = 0; j < imageBox1.Width; j++)
                    {
                        if (canny_frame[0].Data[i, j, 0] == canny_frame[1].Data[i, j, 0] ||
                            canny_frame[0].Data[i, j, 0] == canny_frame[2].Data[i, j, 0])
                        {
                            eq_flag[i, j] = 0;
                        }
                        else if (canny_frame[1].Data[i, j, 0] == canny_frame[2].Data[i, j, 0])
                        {
                            eq_flag[i, j] = 1;
                        }
                        else
                        {
                            eq_flag[i, j] = 2;
                        }
                        switch (eq_flag[i, j])
                        {
                            case 0:
                                canny_out.Data[i, j, 0] = canny_frame[0].Data[i, j, 0];
                                break;
                            case 1:
                                canny_out.Data[i, j, 0] = canny_frame[1].Data[i, j, 0];
                                break;
                            default:
                                canny_out.Data[i, j, 0] = 0;
                                break;
                        }
                    }
                }
                imageBox2.Image = canny_out;
                canny_out._Dilate(1);//形态学滤波：膨胀（3*3矩形结构元素），参数为迭代次数
                cnt = 0;
            }
        }
        /*玻片检测*/
        void SlideDetection()
        {
            Stable_Frame();
            Rectangle_Detection();
            if (!isSlide)
                SlideDetection();
                //startFlag = false;
        }
        void SlideSimilarDetection()
        {
            if(slideIndex==0)
            {
                slide[slideIndex]=tempbox.MinAreaRect();//外接面积最小矩形
                slide_img[slideIndex] = frame.GetSubRect(slide[slideIndex]);
                slide_gray_img[slideIndex] = canny_out.GetSubRect(slide[slideIndex]);
                slideIndex++;
            }
            //距离大于40pixel看作不同slide
            else if (Math.Pow(slide[slideIndex-1].X- tempbox.MinAreaRect().X, 2)+
                Math.Pow(slide[slideIndex - 1].Y - tempbox.MinAreaRect().Y, 2) > 1600)
            {
                slide[slideIndex] = tempbox.MinAreaRect();//外接面积最小矩形
                slide_img[slideIndex] = frame.GetSubRect(slide[slideIndex]);
                slide_gray_img[slideIndex] = canny_out.GetSubRect(slide[slideIndex]);
                slideIndex++;
            }

        }
        /*矩形检测*/
        void Rectangle_Detection()
        {
            
            using (MemStorage storage = new MemStorage()) //allocate storage for contour(轮廓) approximation
                for (
                   Contour<Point> contours = canny_out.FindContours(
                      Emgu.CV.CvEnum.CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_SIMPLE,
                      Emgu.CV.CvEnum.RETR_TYPE.CV_RETR_LIST,
                      storage);
                   contours != null;
                   contours = contours.HNext)
                {
                    Contour<Point> currentContour = contours.ApproxPoly(contours.Perimeter * 0.05, storage);//逼近多边形曲线
                    if (currentContour.Area > size_of_slide_min  &&
                        currentContour.Area < size_of_slide_max && isSlide == false) //only consider contours with area greater than 250
                    {
                        if (currentContour.Total == 4) //The contour has 4 vertices(顶点).
                        {
                            #region determine if all the angles in the contour are within [80, 100] degree
                            bool isRectangle = false;
                            Point[] pts = currentContour.ToArray();
                            LineSegment2D[] edges = PointCollection.PolyLine(pts, true);

                            for (int i = 0; i < edges.Length; i++)
                            {
                                double angle = Math.Abs(
                                   edges[(i + 1) % edges.Length].GetExteriorAngleDegree(edges[i]));
                                if (angle >= 60 || angle <= 120)
                                {
                                    isRectangle = true;
                                    break;
                                }
                            }
                            #endregion
                            if (isRectangle)
                            {
                                slideMinSize.Text = Convert.ToString(size_of_slide_min);
                                tempbox = currentContour.GetMinAreaRect();
                                float tempboxp = tempbox.size.Width/tempbox.size.Height;
                                //根据矩形长宽比判断玻片
                                if (tempboxp > 2.7 && tempboxp < 3.3 )
                                {
                                    SlideSimilarDetection();
                                    if(slideIndex==slideNum)
                                    {
                                        QuickSort(ref slide, 0, slideNum-1);
                                        isSlide = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
        }
        /// <summary>
        /// 将roi坐标转化为摄像头拍摄画面坐标
        /// </summary>
        /// <param name="inRec"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        Rectangle ConvertToFrame(Rectangle recRoi, Rectangle recSlide)
        {
            recRoi.X += recSlide.X;
            recRoi.Y += recSlide.Y;

            return recRoi;
        }
        /// <summary>
        /// roi重复检测
        /// </summary>
        /// <param name="id"></param>
        void RoiSimilarDetection(int id)
        {
            if(roi[id].Count==0)
            {
                roi[id].Add(tempbox.MinAreaRect());
                Image<Bgr, Byte> RectangleImage = slide_img[id];
                RectangleImage.Draw(tempbox.MinAreaRect(), new Bgr(Color.Blue), 2);
                frame.Draw(slide[id], new Bgr(Color.DarkOrange), 2);
                imageBox3.Image = frame;
            }
            //距离大于5pixel看作不同roi    
            else if (Math.Pow(tempbox.MinAreaRect().X - roi[id][roi[id].Count - 1].X, 2)
                + Math.Pow(tempbox.MinAreaRect().Y - roi[id][roi[id].Count - 1].Y, 2) > 25)
            { 
                roi[id].Add(tempbox.MinAreaRect());
                Image<Bgr, Byte> RectangleImage = slide_img[id];
                RectangleImage.Draw(tempbox.MinAreaRect(), new Bgr(Color.Blue), 2);
                frame.Draw(slide[id], new Bgr(Color.DarkOrange), 2);
                imageBox3.Image = frame;   
            }
        }
        /// <summary>
        /// 快速排序(根据横坐标)
        /// </summary>
        /// <param name="inRec">输入矩形列表</param>
        /// <param name="low">下标起点</param>
        /// <param name="high">下标终点</param>
        void QuickSort(ref List<Rectangle> inRec,int low,int high)
        {
            if(low>=high)
                return;
            int i = low;
            int j = high + 1;
            int key = inRec[low].X;
            while(true)
            {
                /*从右向左找比key小的值*/
                while (inRec[--j].X > key)
                {
                    if (j == low)
                        break;
                }
                /*从左向右找比key大的值*/
                while (inRec[++i].X < key)
                {
                    if (i == high)
                        break;
                }
                if (i >= j) break;
                /*交换i,j对应的值*/
                Rectangle temp = inRec[i];
                inRec[i] = inRec[j];
                inRec[j] = temp;
            }
            Rectangle temp2 = inRec[low];
            inRec[low] = inRec[j];
            inRec[j] = temp2;
            QuickSort(ref inRec, low, j - 1);
            QuickSort(ref inRec, j + 1, high);

        }

        void QuickSort(ref Rectangle[] inRec, int low, int high)
        {
            if (low >= high)
                return;
            int i = low;
            int j = high + 1;
            int key = inRec[low].X;
            while (true)
            {
                /*从右向左找比key小的值*/
                while (inRec[--j].X > key)
                {
                    if (j == low)
                        break;
                }
                /*从左向右找比key大的值*/
                while (inRec[++i].X < key)
                {
                    if (i == high)
                        break;
                }
                if (i >= j) break;
                /*交换i,j对应的值*/
                Rectangle temp = inRec[i];
                inRec[i] = inRec[j];
                inRec[j] = temp;
            }
            Rectangle temp2 = inRec[low];
            inRec[low] = inRec[j];
            inRec[j] = temp2;
            QuickSort(ref inRec, low, j - 1);
            QuickSort(ref inRec, j + 1, high);

        }
        /*ROI检测*/
        void RegionDetection(int id)
        {
            //allocate storage for contour(轮廓) approximation
            using (MemStorage storage = new MemStorage())
                for (
                   Contour<Point> contours = slide_gray_img[id].FindContours(
                      Emgu.CV.CvEnum.CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_SIMPLE,
                      Emgu.CV.CvEnum.RETR_TYPE.CV_RETR_LIST,
                      storage);
                   contours != null;
                   contours = contours.HNext)
                {
                    //逼近多边形曲线
                    Contour<Point> currentContour = contours.ApproxPoly(contours.Perimeter * 0.05, storage);
                    //only consider contours with area greater than 250
                    if (size_of_roi_min < currentContour.Area && 
                        currentContour.Area < size_of_roi_max) 
                    {
                        tempbox = currentContour.GetMinAreaRect();
                        //保证不会把标签和其他东西误检为roi
                        if(tempbox.center.X < slide[id].Width * 0.6
                           && tempbox.center.X > slide[id].Width * 0.15)
                        {
                            if (roi[id].Count < roiNum)
                                RoiSimilarDetection(id);
                            else
                                break;
                            
                        }
                    }
                }
            if (roi[id].Count == 0)
            {
                SlideDetection();
                RegionDetection(id);
            }
            else
            {
                QuickSort(ref roi[id],0,roi[id].Count-1);
                scanDone = true;
                //startFlag = false;
            }
                
        }

        private void Canny_th_up_Click(object sender, EventArgs e)
        {
            th1+=5;
            canThBar1.Value = th1;
            cannyBox1.Text = Convert.ToString(th1);
        }

        private void Canny_th_down_Click(object sender, EventArgs e)
        {
            th1-=5;
            canThBar1.Value = th1;
            cannyBox1.Text = Convert.ToString(th1);
        }

        private void Canny_th_up2_Click(object sender, EventArgs e)
        {
            th2+=5;
            canThBar2.Value = th2;
            cannyBox2.Text = Convert.ToString(th2);
        }

        private void Canny_th_down2_Click(object sender, EventArgs e)
        {
            th2-=5;
            canThBar2.Value = th2;
            cannyBox2.Text = Convert.ToString(th2);
        }

        private void Slide_Size_Up_Click(object sender, EventArgs e)
        {
            size_of_slide_min += 5;
            slideMinSizeBar.Value = size_of_slide_min;
            slideMinSize.Text = Convert.ToString(size_of_slide_min);
        }

        private void Slide_Size_Down_Click(object sender, EventArgs e)
        {
            size_of_slide_min -= 5;
            slideMinSizeBar.Value = size_of_slide_min;
            slideMinSize.Text = Convert.ToString(size_of_slide_min);
        }

        public class TaskDesc
        {
            public string filedir;
            public DateTime dt;
            public int slideid;

            public Rectangle rectRoi;
            public RectangleF rectRoiF;
            public PointF[] rectRoiPts;
            public Size rectRoiSize;

            public Rectangle rectBlk;
            public RectangleF rectBlkF;
            public PointF[] rectBlkPts;
            public Size rectBlkSize;

            public TaskDesc(string filedir, Rectangle rectRoi, RectangleF rectRoiF, PointF[] rectRoiPts, Size rectRoiSize,
                                            Rectangle rectBlk, RectangleF rectBlkF, PointF[] rectBlkPts, Size rectBlkSize,
                            int slideid)
            {
                this.filedir = filedir;
                this.dt = DateTime.Now;
                this.slideid = slideid;

                this.rectRoi = rectRoi;
                this.rectRoiF = rectRoiF;
                this.rectRoiPts = rectRoiPts;
                this.rectRoiSize = rectRoiSize;

                this.rectBlk = rectBlk;
                this.rectBlkF = rectBlkF;
                this.rectBlkPts = rectBlkPts;
                this.rectBlkSize = rectBlkSize;
            }
        }

        public class RectConverter
        {

            static public string[] eyepieces = new string[4] { "10x", "20x", "40x", "100x" };
            static public int[] ieyepieces = new int[4] { 10, 20, 40, 100 };
            static public float xstep_20x = 0.32F;
            static public float ystep_20x = 0.23F;
            static public float xstep_40x = 0.15F;
            static public float ystep_40x = 0.11F;

            static public Rectangle rect1st = new Rectangle(77, 77, 247, 500);
            static public Rectangle rect2nd = new Rectangle(477, 77, 247, 500);
            static public Rectangle rect3rd = new Rectangle(877, 77, 247, 500);
            static public Rectangle rect4th = new Rectangle(1277, 77, 247, 500);

            static public Rectangle rect1stROI = new Rectangle(77, 227, 247, 250);
            static public Rectangle rect2ndROI = new Rectangle(477, 227, 247, 250);
            static public Rectangle rect3rdROI = new Rectangle(877, 227, 247, 250);
            static public Rectangle rect4thROI = new Rectangle(1277, 227, 247, 250);


            /* 原始数据
            public PointF lu1 = new PointF(121.689F, 73.5981F);
            public PointF ld1 = new PointF(121.66F, 27.1874F);
            public PointF rd1 = new PointF(96.5164F, 27.236F);
            public PointF ru1 = new PointF(96.3882F, 76.8784F);

            public PointF lu2 = new PointF(93.3491F, 74.125F);
            public PointF ld2 = new PointF(93.3599F, 27.1964F);
            public PointF rd2 = new PointF(68.2347F, 27.252F);
            public PointF ru2 = new PointF(68.0632F, 75.0527F);
            */

            // 修正数据
            static public PointF lu1 = new PointF(121.5014F, 67.9150F);
            static public PointF ld1 = new PointF(121.5014F, 21.5745F);
            static public PointF rd1 = new PointF(96.5038F, 21.5745F);
            static public PointF ru1 = new PointF(96.5038F, 67.9150F);

            static public PointF lu2 = new PointF(93.1812F, 67.9150F);
            static public PointF ld2 = new PointF(93.1812F, 21.5745F);
            static public PointF rd2 = new PointF(69.2115F, 21.5745F);
            static public PointF ru2 = new PointF(69.2115F, 67.9150F);

            static public PointF lu3 = new PointF(64.8728F, 67.9150F);
            static public PointF ld3 = new PointF(64.8728F, 21.5745F);
            static public PointF rd3 = new PointF(39.9191F, 21.5745F);
            static public PointF ru3 = new PointF(39.9191F, 67.9150F);

            static public PointF lu4 = new PointF(34.3033F, 67.9150F);
            static public PointF ld4 = new PointF(34.3033F, 21.5745F);
            static public PointF rd4 = new PointF(11.6354F, 21.5745F);
            static public PointF ru4 = new PointF(11.6354F, 67.9150F);

            static public RectangleF frect1st = new RectangleF(rd1, new SizeF(Math.Abs(ru1.X - lu1.X), Math.Abs(rd1.Y - ru1.Y)));
            static public RectangleF frect2nd = new RectangleF(rd2, new SizeF(Math.Abs(ru2.X - lu2.X), Math.Abs(rd2.Y - ru2.Y)));
            static public RectangleF frect3rd = new RectangleF(rd3, new SizeF(Math.Abs(ru3.X - lu3.X), Math.Abs(rd3.Y - ru3.Y)));
            static public RectangleF frect4th = new RectangleF(rd4, new SizeF(Math.Abs(ru4.X - lu4.X), Math.Abs(rd4.Y - ru4.Y)));

            static public RectangleF[] frects = new RectangleF[4] {
            frect1st, frect2nd, frect3rd, frect4th
        };

            static public Rectangle[] rects = new Rectangle[4] {
            rect1st, rect2nd, rect3rd, rect4th
        };

            public static RectangleF ConvertFromDrawRect(Rectangle rectangle, Rectangle recSlide, int idx)
            {
                float yratio = (float)rectangle.X / (float)recSlide.Width;
                float xratio = (float)rectangle.Y / (float)recSlide.Height;
                float hratio = (float)rectangle.Width / (float)recSlide.Width;
                float wratio = (float)rectangle.Height / (float)recSlide.Height;

                float x, y, w, h;
                x = frects[idx].X + frects[idx].Width * (1.0F - xratio);
                y = frects[idx].Y + frects[idx].Height * (1.0F - yratio);
                w = frects[idx].Width * wratio;
                h = frects[idx].Height * hratio;

                return new RectangleF(x, y, w, h);
            }
            /// <summary>
            /// 将roi转化成软件界面坐标
            /// </summary>
            /// <param name="rectangle"></param>
            /// <param name="recSlide"></param>
            /// <param name="idx"></param>
            /// <returns></returns>
            public static Rectangle ConvertToDrawRect(Rectangle rectangle, Rectangle recSlide, int idx)
            {
                Rectangle retRect=new Rectangle();
                float yratio = (float)rectangle.X / (float)recSlide.Width;
                float xratio = (float)rectangle.Y / (float)recSlide.Height;
                float hratio = (float)rectangle.Width / (float)recSlide.Width;
                float wratio = (float)rectangle.Height / (float)recSlide.Height;

                retRect.X = Convert.ToInt32(rects[idx].X + rects[idx].Width * xratio);
                retRect.Y = Convert.ToInt32(rects[idx].Y + rects[idx].Height * yratio);
                retRect.Width = Convert.ToInt32(rects[idx].Width * wratio);
                retRect.Height = Convert.ToInt32(rects[idx].Height * hratio);

                return retRect;
            }

            public static Size GetStepsByRectangleF(RectangleF frectangle, int amp)
            {
                float x_step = 0.0F;
                float y_step = 0.0F;

                if (amp == 1)
                {
                    x_step = xstep_20x;
                    y_step = ystep_20x;
                }
                else if (amp == 2)
                {
                    x_step = xstep_40x;
                    y_step = ystep_40x;
                }

                Size size = new Size();
                size.Width = Convert.ToInt32(frectangle.Width / x_step);
                size.Height = Convert.ToInt32(frectangle.Height / y_step);
                return size;
            }


            public static PointF[] GetAllPointsByRectangleF(RectangleF frectangle, int amp)
            {
                float x_step = 0.0F;
                float y_step = 0.0F;

                if (amp == 1)
                {
                    x_step = xstep_20x;
                    y_step = ystep_20x;
                }
                else if (amp == 2)
                {
                    x_step = xstep_40x;
                    y_step = ystep_40x;
                }

                Size size = GetStepsByRectangleF(frectangle, amp);
                PointF[] points = new PointF[size.Width * size.Height];
                PointF stP = new PointF(frectangle.X, frectangle.Y);
                Int32 xstep = 1;
                Int32 x = 0;
                Int32 ptr = 0;

                foreach (Int32 y in Enumerable.Range(0, size.Height))
                {

                    if (y % 2 == 1)
                    {
                        x = 0; xstep = 1;
                    }
                    else
                    {
                        x = size.Width - 1; xstep = -1;
                    }
                    foreach (Int32 u in Enumerable.Range(0, size.Width))
                    {
                        PointF pt = new PointF(stP.X - x * x_step, stP.Y + y * y_step);
                        points[ptr] = pt;
                        ptr++;
                        x = x + xstep;
                    }
                }
                return points;
            }

            public static Point ConvertPointFToPoint(Rectangle rect, RectangleF frect, PointF pt)
            {
                Point ipt = new Point();
                ipt.X = Convert.ToInt32(rect.X + rect.Width * ((frect.X - pt.X) / frect.Width));
                ipt.Y = Convert.ToInt32(rect.Y + rect.Height * ((frect.Y - pt.Y) / frect.Height));
                return ipt;
            }

            public class TaskDesc
            {
                public string filedir;
                public DateTime dt;
                public int slideid;

                public Rectangle rectRoi;
                public RectangleF rectRoiF;
                public PointF[] rectRoiPts;
                public Size rectRoiSize;

                public Rectangle rectBlk;
                public RectangleF rectBlkF;
                public PointF[] rectBlkPts;
                public Size rectBlkSize;

                public TaskDesc(string filedir, Rectangle rectRoi, RectangleF rectRoiF, PointF[] rectRoiPts, Size rectRoiSize,
                                                Rectangle rectBlk, RectangleF rectBlkF, PointF[] rectBlkPts, Size rectBlkSize,
                                int slideid)
                {
                    this.filedir = filedir;
                    this.dt = DateTime.Now;
                    this.slideid = slideid;

                    this.rectRoi = rectRoi;
                    this.rectRoiF = rectRoiF;
                    this.rectRoiPts = rectRoiPts;
                    this.rectRoiSize = rectRoiSize;

                    this.rectBlk = rectBlk;
                    this.rectBlkF = rectBlkF;
                    this.rectBlkPts = rectBlkPts;
                    this.rectBlkSize = rectBlkSize;
                }
            }

            public static string GenerateTaskDesc(string filedir, string filename,
                Rectangle rectRoi, RectangleF rectRoiF, PointF[] rectRoiPts, Size rectRoiSize,
                Rectangle rectBlk, RectangleF rectBlkF, PointF[] rectBlkPts, Size rectBlkSize, int slideid)
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.NullValueHandling = NullValueHandling.Ignore;
                string taskdesc = Path.Combine(filedir, filename + ".dsc");
                using (StreamWriter sw = new StreamWriter(taskdesc))
                {
                    using (JsonWriter jw = new JsonTextWriter(sw))
                    {
                        serializer.Serialize(jw, new TaskDesc(taskdesc,
                            rectRoi, rectRoiF, rectRoiPts, rectRoiSize,
                            rectBlk, rectBlkF, rectBlkPts, rectBlkSize,
                            slideid));
                    }
                }
                return filedir;
            }

            public static string GenerateTaskDesc(string filedir, string filename, TaskDesc tdsc)
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.NullValueHandling = NullValueHandling.Ignore;
                string taskdesc = Path.Combine(filedir, filename + ".dsc");
                using (StreamWriter sw = new StreamWriter(taskdesc))
                {
                    using (JsonWriter jw = new JsonTextWriter(sw))
                    {
                        serializer.Serialize(jw, new TaskDesc(taskdesc,
                            tdsc.rectRoi, tdsc.rectRoiF, tdsc.rectRoiPts, tdsc.rectRoiSize,
                            tdsc.rectBlk, tdsc.rectBlkF, tdsc.rectBlkPts, tdsc.rectBlkSize,
                            tdsc.slideid));
                    }
                }
                return filedir;
            }

            public static TaskDesc RestoreTaskDesc(string taskdesc)
            {
                TaskDesc tdsc;

                using (StreamReader sw = new StreamReader(taskdesc))
                {
                    tdsc = JsonConvert.DeserializeObject<TaskDesc>(sw.ReadToEnd());
                }

                return tdsc;
            }


        }

        void GenerateTaskDesc(string filedir,
            Rectangle rectRoi, RectangleF rectRoiF, PointF[] rectRoiPts, Size rectRoiSize,
            Rectangle rectBlk, RectangleF rectBlkF, PointF[] rectBlkPts, Size rectBlkSize, int slideid)
        {
            JsonSerializer serializer = new JsonSerializer();
            serializer.NullValueHandling = NullValueHandling.Ignore;
            using (StreamWriter sw = new StreamWriter(filedir))
            {
                using (JsonWriter jw = new JsonTextWriter(sw))
                {
                    serializer.Serialize(jw, new TaskDesc(filedir,
                        rectRoi, rectRoiF, rectRoiPts, rectRoiSize,
                        rectBlk, rectBlkF, rectBlkPts, rectBlkSize,
                        slideid));
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            SaveFileDialog fileDialog = new SaveFileDialog();
            //fileDialog.Filter = "任务描述文件（*.dsc）|*.dsc";
            fileDialog.FilterIndex = 1;
            fileDialog.ShowDialog();
            this.filedirBox.Text = fileDialog.FileName;
            fileDir = fileDialog.FileName;
            filedirChoose = true;
        }

        void GenerateDsc(Rectangle inRec,Rectangle inSlide,int roiId,int id)
        {
            RectangleF rectFRoiRecord;
            PointF[] rectFRoiPoints;
            Size rectRoiSize;

            RectangleF rectFBlkRecord;
            Rectangle rectBlkRecord;
            PointF[] rectFBlkPoints;
            Size rectBlkSize;
            string fileName = fileDir +"-"+ Convert.ToString(id) 
                + "-roi" + Convert.ToString(roiId) + ".dsc";

            rectFRoiRecord = RectConverter.ConvertFromDrawRect(inRec, inSlide, id);
            rectFRoiPoints = RectConverter.GetAllPointsByRectangleF(rectFRoiRecord, amp);
            rectRoiSize = RectConverter.GetStepsByRectangleF(rectFRoiRecord, amp);

            rectBlkRecord = rectsBLK[id];
            rectFBlkRecord = RectConverter.ConvertFromDrawRect(rectsBLK[id], inSlide, id);
            // Cal two Points.
            rectFBlkPoints = new PointF[2];
            rectFBlkPoints[0] = new PointF(rectFBlkRecord.Left - rectFBlkRecord.Width / 3, rectFBlkRecord.Top + rectFBlkRecord.Height / 2);
            rectFBlkPoints[1] = new PointF(rectFBlkRecord.Left - rectFBlkRecord.Width * 2 / 3, rectFBlkRecord.Top + rectFBlkRecord.Height / 2);
            rectBlkSize = RectConverter.GetStepsByRectangleF(rectFBlkRecord, amp);

            inRec = RectConverter.ConvertToDrawRect(inRec, inSlide, id);

            GenerateTaskDesc(fileName, inRec, rectFRoiRecord, rectFRoiPoints, rectRoiSize,
                            rectBlkRecord, rectFBlkRecord, rectFBlkPoints, rectBlkSize,
                            id);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if(scanDone && ampChoose && filedirChoose)
            {
                for(int j=0;j<slideNum;j++)
                {
                    for (int i = 0; i < roi[j].Count; i++)
                    {
                        GenerateDsc(roi[j][i], slide[j], i, j);
                    }
                }
                for(int i=0;i<slideNum;i++)
                    roi[i].Clear();
                slideIndex = 0;
                scanDone = false;
                isSlide = false;
                startFlag = false;
                //frame = null;
                MessageBox.Show("扫描完成", "Message");
            }
            else if(!scanDone)
            {
                MessageBox.Show("请等待完成扫描","Message");
            }
            else if(!ampChoose)
            {
                MessageBox.Show("请选择物镜倍数", "Message");
            }
            else if (!filedirChoose)
            {
                MessageBox.Show("请选择保存路径", "Message");
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch(comboBox1.SelectedIndex)
            {
                case 0://20x
                    amp = 1;
                    ampChoose = true;
                    break;
                case 1://40x
                    amp = 2;
                    ampChoose = true;
                    break;
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if(slideNum>4)
                MessageBox.Show("请选择扫描个数", "Message");
            else if(roiNum==0)
                MessageBox.Show("请选择区域个数", "Message");
            else
                startFlag = true;
        }

        private void canThBar1_Scroll(object sender, EventArgs e)
        {
            th1 = canThBar1.Value;
            cannyBox1.Text = Convert.ToString(th1);
        }

        private void canThBar2_Scroll(object sender, EventArgs e)
        {
            th2 = canThBar2.Value;
            cannyBox2.Text = Convert.ToString(th2);
        }

        private void slideSizeBar_Scroll(object sender, EventArgs e)
        {
            size_of_slide_min = slideMinSizeBar.Value;
            slideMinSize.Text = Convert.ToString(size_of_slide_min);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            startFlag = false;
            scanDone = false;
            isSlide = false;
            slideIndex = 0;
            frame = null;
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            slideNum = comboBox2.SelectedIndex + 1;
        }

        private void roiSizeBar_Scroll(object sender, EventArgs e)
        {
            size_of_roi_max = roiMaxSizeBar.Value;
            roiMaxSize.Text = Convert.ToString(size_of_roi_max);
        }

        private void roiSizeUp_Click(object sender, EventArgs e)
        {
            size_of_roi_max += 5;
            roiMaxSizeBar.Value = size_of_roi_max;
            roiMaxSize.Text = Convert.ToString(size_of_roi_max);
        }

        private void roiSizeDown_Click(object sender, EventArgs e)
        {
            size_of_roi_max -= 5;
            roiMaxSizeBar.Value = size_of_roi_max;
            roiMaxSize.Text = Convert.ToString(size_of_roi_max);
        }

        private void comboBox3_SelectedIndexChanged(object sender, EventArgs e)
        {
            roiNum = comboBox3.SelectedIndex + 1;
        }

        private void button8_Click(object sender, EventArgs e)
        {
            size_of_slide_max += 5;
            slideMinSizeBar.Value = size_of_slide_max;
            slideMinSize.Text = Convert.ToString(size_of_slide_max);
        }

        private void button7_Click(object sender, EventArgs e)
        {
            size_of_slide_max -= 5;
            slideMinSizeBar.Value = size_of_slide_max;
            slideMinSize.Text = Convert.ToString(size_of_slide_max);
        }

        private void button6_Click(object sender, EventArgs e)
        {
            size_of_roi_min += 5;
            roiMinSizeBar.Value = size_of_roi_min;
            roiMinSize.Text = Convert.ToString(size_of_roi_min);
        }

        private void button5_Click(object sender, EventArgs e)
        {
            size_of_roi_min -= 5;
            roiMinSizeBar.Value = size_of_roi_min;
            roiMinSize.Text = Convert.ToString(size_of_roi_min);
        }

        private void slideMaxSizeBar_Scroll(object sender, EventArgs e)
        {
            size_of_slide_max = slideMaxSizeBar.Value;
            slideMaxSize.Text = Convert.ToString(size_of_slide_max);
        }

        private void roiMinSizeBar_Scroll(object sender, EventArgs e)
        {
            size_of_roi_min = roiMinSizeBar.Value;
            roiMinSize.Text = Convert.ToString(size_of_roi_min);
        }
    }
}
