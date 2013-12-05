/*
 * Right click in the left panel to paint. Left click to erase.
 * You can add, remove, and change hidden layers to the network using the list box and control buttons beneath it.
 * When ready, click the button with a number in it to train the neural net and see its results.
 *    Note that the net is trying to replicate on the right panel what you've painted on the left
 * Blue dots indicate the points on your image used for the net's training data. Also used to calculate error.
 *    Dots can be moved by adjusting the dial at the bottom right of the panel (bottom left of control console)
 * To train more than one epoch at a time, change the dial below the training button (the button with the number in it)
 * 
 * And that's basically it. Play and enjoy. And learn too. Learning's good.
 * 
 * David Maxson
 * scnerd@gmail.com
 * 12/4/13
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Encog.ML.Data.Basic;
using Encog.Neural.Networks;
using Encog.Neural.Networks.Layers;
using Encog.Neural.Networks.Structure;
using Encog.Neural.Networks.Training.Propagation.Resilient;

namespace RedVsGreen
{
    public partial class Form1 : Form
    {
        int width, height;
        Bitmap actual_img, granularity_mask;
        Graphics actual_img_draw, granularity_mask_draw;

        Color yes_color = Color.LightGreen, no_color = Color.Salmon, sample_color = Color.Blue;
        Brush yes_brush, no_brush, sample_brush;
        int brush_sz = 25, sample_sz = 4;

        BasicNetwork network;
        ResilientPropagation trainer;

        bool mouse_down = false;
        bool mouse_button = false; // false == left, true == right

        List<Point> sample_pts = new List<Point>();

        double[][] prev_in, prev_out;

        public Form1()
        {
            InitializeComponent();
            width = picMain.Width;
            height = picMain.Height;
            actual_img = new Bitmap(width, height);
            granularity_mask = new Bitmap(width, height);
            yes_brush = new SolidBrush(yes_color);
            no_brush = new SolidBrush(no_color);
            sample_brush = new SolidBrush(sample_color);

            actual_img_draw = Graphics.FromImage(actual_img);
            actual_img_draw.Clear(yes_color);
            granularity_mask_draw = Graphics.FromImage(granularity_mask);
            ResetGranularity();

            network = new BasicNetwork();

            picMain.CreateGraphics().Clear(yes_color);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            MarkModified();
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            lstLayers.Items.Add(1);

            MarkModified();
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            lstLayers.Items.Remove(lstLayers.SelectedItem);

            MarkModified();
        }

        private void btnUp_Click(object sender, EventArgs e)
        {
            var ind = lstLayers.SelectedIndex;
            if (ind == -1 || ind == 0) return;
            var removed = lstLayers.SelectedItem;
            lstLayers.Items.Remove(removed);
            lstLayers.Items.Insert(ind - 1, removed);
            lstLayers.SelectedIndex = ind - 1;

            MarkModified();
        }

        private void btnDown_Click(object sender, EventArgs e)
        {
            var ind = lstLayers.SelectedIndex;
            if (ind == -1 || ind == lstLayers.Items.Count - 1) return;
            var removed = lstLayers.SelectedItem;
            lstLayers.Items.Remove(removed);
            lstLayers.Items.Insert(ind + 1, removed);
            lstLayers.SelectedIndex = ind + 1;

            MarkModified();
        }

        private void btnIncrement_Click(object sender, EventArgs e)
        {
            if (lstLayers.SelectedIndex == -1) return;
            lstLayers.Items[lstLayers.SelectedIndex] = (int)lstLayers.SelectedItem + 1;

            MarkModified();
        }

        private void btnDecrement_Click(object sender, EventArgs e)
        {
            if (lstLayers.SelectedIndex == -1) return;
            lstLayers.Items[lstLayers.SelectedIndex] = Math.Max(1, (int)lstLayers.SelectedItem - 1);

            MarkModified();
        }

        private void numGranularity_ValueChanged(object sender, EventArgs e)
        {
            ResetGranularity();

            MarkModified();
        }

        private void ResetGranularity()
        {
            // Reset points
            int granularity = (int)numGranularity.Value;

            var xs = Enumerable.Range(0, (int)Math.Ceiling(width / (double)granularity)).Select(i => i * granularity);
            var ys = Enumerable.Range(0, (int)Math.Ceiling(height / (double)granularity)).Select(i => i * granularity);

            sample_pts = (from x in xs
                          from y in ys
                          select new Point(x, y)).ToList();

            // Reset mask
            granularity_mask_draw.Clear(Color.FromArgb(0));

            foreach (Point p in sample_pts)
            {
                granularity_mask_draw.FillEllipse(sample_brush, p.X - sample_sz / 2, p.Y - sample_sz / 2, sample_sz, sample_sz);
            }
        }

        private void picMain_MouseDown(object sender, MouseEventArgs e)
        {
            mouse_down = true;
            mouse_button = e.Button == System.Windows.Forms.MouseButtons.Right ? true : false;

            MarkModified();
        }

        private void picMain_MouseUp(object sender, MouseEventArgs e)
        {
            mouse_down = false;

            MarkModified();
        }

        private void picMain_MouseMove(object sender, MouseEventArgs e)
        {
            if (mouse_down)
            {
                actual_img_draw.FillEllipse(mouse_button ? no_brush : yes_brush, e.X - brush_sz / 2, e.Y - brush_sz / 2, brush_sz, brush_sz);

                MarkModified();
            }
        }

        private void MarkModified()
        {
            btnEpoch.Text = "0";
            picMain.CreateGraphics().DrawImageUnscaled(actual_img, 0, 0);
            picMain.CreateGraphics().DrawImageUnscaled(granularity_mask, 0, 0);
        }

        private void CreateData(out double[][] inputs, out double[][] outputs)
        {
            inputs = sample_pts.Select<Point, double[]>(p => new double[] { p.X / (double)width, p.Y / (double)height }).ToArray();
            outputs = sample_pts.Select<Point, double[]>(p => new double[] { ColorEquals(actual_img.GetPixel(p.X, p.Y), yes_color) ? 1d : 0d }).ToArray();
        }

        private bool ColorEquals(Color a, Color b)
        {
            return Math.Max(Math.Max(
                Math.Abs(a.R - b.R),
                Math.Abs(a.G - b.G)),
                Math.Abs(a.B - b.B)) <= 1;
        }

        private void btnEpoch_Click(object sender, EventArgs e)
        {
            // Create data
            CreateData(out prev_in, out prev_out);
            var dataset = new BasicMLDataSet(prev_in, prev_out);
            //System.IO.File.WriteAllText(@"C:\Users\David\EncogProjects\MyEncogProject\img_data.csv",
            //    String.Join("\r\n", Enumerable.Range(0, prev_in.Length).Select(i => String.Join(",", prev_in[i][0], prev_in[i][1], prev_out[i][0]))));

            if (btnEpoch.Text == "0")
            {
                // Reset network
                network = new BasicNetwork();
                network.AddLayer(new BasicLayer(2));
                foreach (var item in lstLayers.Items)
                    network.AddLayer(new BasicLayer(int.Parse(item.ToString())));
                network.AddLayer(new BasicLayer(1));
                network.Structure.FinalizeStructure();
                network.Reset();

                //trainer = new ResilientPropagation(network, dataset);
                trainer = new ResilientPropagation(network, dataset);
            }

            trainer.Iteration((int)numEpochs.Value);
            lblError.Text = String.Format("Error: {0:f2}%", trainer.Error * 100);
            btnEpoch.Text = trainer.IterationNumber.ToString();

            DisplayNet();
        }

        private void DisplayNet()
        {
            picOut.SuspendLayout();
            Graphics g = picOut.CreateGraphics();
            //double[] outputs = (
            //                  from y in Enumerable.Range(0, height)
            //                  from x in Enumerable.Range(0, width)
            //                  select network.Compute(new BasicMLData(new double[] { x, y }))[0]).ToArray();

            //for (int y = 0; y < height; y++)

            var xs = Enumerable.Range(0, width);

            for(int y = 0; y < height; y++)
            {
                    foreach (int x in xs)
                        g.FillRectangle(new SolidBrush(Blend(yes_color, no_color, Solve(x,y))), x, y, 1, 1);
            }
            picOut.ResumeLayout();
        }

        //http://stackoverflow.com/questions/3722307/is-there-an-easy-way-to-blend-two-system-drawing-color-values
        public static Color Blend(Color color, Color backColor, double amount)
        {
            amount = Math.Min(Math.Max(amount, 0), 1);
            byte r = (byte)((color.R * amount) + backColor.R * (1 - amount));
            byte g = (byte)((color.G * amount) + backColor.G * (1 - amount));
            byte b = (byte)((color.B * amount) + backColor.B * (1 - amount));
            return Color.FromArgb(r, g, b);
        }

        bool ignore_value_change = false;
        private void numLayerSize_ValueChanged(object sender, EventArgs e)
        {
            if (lstLayers.SelectedIndex == -1) return;
            lstLayers.Items[lstLayers.SelectedIndex] = Math.Max(1, (int)numLayerSize.Value);

            if(ignore_value_change)
            MarkModified();
            ignore_value_change = false;
        }

        private double Solve(double x, double y)
        {
            return network.Compute(new BasicMLData(new double[] { x / (double)width, y / (double)height }))[0];
        }

        private void lstLayers_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstLayers.SelectedIndex == -1) return;
            ignore_value_change = true;
            numLayerSize.Value = (int)lstLayers.SelectedItem;
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            try
            {
                MarkModified();
            }
            catch
            { }
        }
    }
}
