using System.Diagnostics;

namespace PaintApp
{
    public partial class Form1 : Form
    {
        private Bitmap MyBitMap;
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_MouseMove(object sender, MouseEventArgs e)
        {
            MyBitMap = new Bitmap(this.ClientSize.Width, this.ClientSize.Height);
            Debug.WriteLine($"Mouse Position: X={e.X}, Y={e.Y}");
        }
    }
}
