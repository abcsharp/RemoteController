using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using SuperWebSocket;

namespace Server
{
	public partial class Form2 : Form
	{
		bool scrollMode;
		Form1 parent;

		public Form2(string name,Bitmap bitmap)
		{
			InitializeComponent();
			parent=Application.OpenForms.Cast<Form>().First(f=>f is Form1) as Form1;
			Name=name;
			Text=Name+"さんの画面";
			Shown+=(o,e)=>UpdateScreenShot(bitmap);
			timer1.Enabled=checkBox1.Checked;
			scrollMode=false;
		}

		private void Form2_FormClosing(object sender,FormClosingEventArgs e)
		{
			pictureBox1.Image.Dispose();
		}

		private void button1_Click(object sender,EventArgs e)
		{
			scrollMode=!scrollMode;
			button1.Text=scrollMode?"全体表示":"拡大表示";
			pictureBox1.SizeMode=scrollMode?PictureBoxSizeMode.AutoSize:PictureBoxSizeMode.Zoom;
			pictureBox1.Dock=scrollMode?DockStyle.None:DockStyle.Fill;
		}

		private void checkBox1_CheckedChanged(object sender,EventArgs e)
		{
			timer1.Enabled=checkBox1.Checked;
		}

		private void timer1_Tick(object sender,EventArgs e)
		{
			parent.RequestScreenShot(new[]{Name},0,0);
		}

		public void UpdateScreenShot(Bitmap bitmap)
		{
			if(pictureBox1.Image!=null){
				pictureBox1.Image.Dispose();
				pictureBox1.Image=null;
			}
			pictureBox1.Image=bitmap;
			GC.Collect();
		}

		private void pictureBox1_DoubleClick(object sender,EventArgs e)
		{
			parent.RequestScreenShot(new[]{Name},0,0);
		}
	}
}
