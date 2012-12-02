using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using SuperWebSocket;

namespace Server
{
	public partial class Form3 : Form
	{
		Form1 parent;

		public Form3(string[] users,Size size)
		{
			InitializeComponent();
			parent=Application.OpenForms.Cast<Form>().First(f=>f is Form1) as Form1;
			imageList1.ImageSize=size;
			Name="";
			foreach(var user in users) AddUser(user);
		}

		private void contextMenuStrip1_Opening(object sender,CancelEventArgs e)
		{
			toolStripMenuItem1.Enabled=listView1.SelectedIndices.Count>0;
		}

		private void toolStripMenuItem1_Click(object sender,EventArgs e)
		{
			var users=from item in listView1.SelectedItems.Cast<ListViewItem>() select item.Name;
			parent.RequestScreenShot(users.ToArray(),0,0);
		}

		private void timer1_Tick(object sender,EventArgs e)
		{
			var users=from item in listView1.Items.Cast<ListViewItem>() select item.Name;
			parent.RequestScreenShot(users.ToArray(),imageList1.ImageSize.Width,imageList1.ImageSize.Height);
		}

		public void UpdateThumbnail(string name,Bitmap bitmap)
		{
			if(imageList1.Images[name]!=null) imageList1.Images[name].Dispose();
			imageList1.Images.RemoveByKey(name);
			imageList1.Images.Add(name,bitmap);
		}

		public void RemoveUser(string name)
		{
			listView1.Items.RemoveByKey(name);
			if(imageList1.Images[name]!=null) imageList1.Images[name].Dispose();
			imageList1.Images.RemoveByKey(name);
		}

		public void AddUser(string name)
		{
			var item=new ListViewItem(name,name);
			item.Name=name;
			listView1.Items.Add(item);
			parent.RequestScreenShot(new[]{name},imageList1.ImageSize.Width,imageList1.ImageSize.Height);
		}

		private void Form3_Shown(object sender,EventArgs e)
		{
			timer1.Enabled=true;
		}

		private void Form3_FormClosing(object sender,FormClosingEventArgs e)
		{
			timer1.Enabled=false;
		}

		private void listView1_DoubleClick(object sender,EventArgs e)
		{
			var users=from item in listView1.SelectedItems.Cast<ListViewItem>() select item.Name;
			parent.RequestScreenShot(users.ToArray(),0,0);
		}
	}
}
