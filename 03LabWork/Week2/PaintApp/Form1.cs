using System.Diagnostics;

namespace PaintApp
{
    public partial class Form1 : Form
    {
        private Bitmap MyBitMap;
        private Boolean mouseDown = false;

        public Form1()
        {
            MyBitMap = new Bitmap(this.ClientSize.Width, this.ClientSize.Height);
            InitializeComponent();
        }

        private void Form1_MouseMove(object sender, MouseEventArgs e)
        {
            if (!mouseDown) return;
            Graphics g = Graphics.FromImage(MyBitMap);
            Pen MyPen = new Pen(Color.Black, 2);
            g.DrawLine(MyPen, e.X, e.Y, e.X + 1, e.Y + 1);
            Debug.WriteLine($"Mouse Position: X={e.X}, Y={e.Y}");
            Form1_Paint(sender, new PaintEventArgs(this.CreateGraphics(), this.ClientRectangle));
        }

        private void Form1_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.DrawImageUnscaled(MyBitMap, 0, 0);
        }

        private void Form1_MouseDown(object sender, MouseEventArgs e)
        {
            mouseDown = true;
        }

        private void Form1_MouseUp(object sender, MouseEventArgs e)
        {
            mouseDown = false;
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }
}

