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
using System.Net.NetworkInformation;
using SuperWebSocket;

namespace Server
{
	public partial class Form1 : Form
	{
		MessageServer server;
		Color[] colors;
		string localAddress;
		readonly Size thumbnailSize=new Size(200,150);
		Form3 screenShotListView;
		List<string> commandHistory;
		const int maxHistoryCount=20;
		int currentHistory;

		public Form1()
		{
			InitializeComponent();
			screenShotListView=null;
			Name="";
			currentHistory=-1;
			commandHistory=new List<string>();
			server=new MessageServer();
			server.NewUserAdded+=OnNewUserAdded;
			server.UserRemoved+=OnUserRemoved;
			server.MessageReceived+=OnMessageReceived;
			server.ScreenShotReceived+=OnScreenShotReceived;
			GetUserColors();
			localAddress=GetLocalIPAddress();
			if(localAddress==null){
				MessageBox.Show("ネットワークに接続していません。","",MessageBoxButtons.OK,MessageBoxIcon.Error);
				Application.Exit();
			}
			server.Start();
			UpdateTitle();
		}

		void Form1_FormClosing(object sender,FormClosingEventArgs e)
		{
			server.UserRemoved-=OnUserRemoved;
			server.Stop();
		}

		void button1_Click(object sender,EventArgs e)
		{
			if(textBox1.Text!=""){
				var targets=from user in listBox1.SelectedItems.Cast<User>() select user.Name;
				if(targets.Count()==0) targets=from user in listBox1.Items.Cast<User>() select user.Name;
				if(radioButton1.Checked) server.SendOpenRequest(textBox1.Text,targets.ToArray());
				else if(radioButton2.Checked){
					var splittedCommand=ParseCommand(textBox1.Text);
					server.SendExecuteRequest(splittedCommand[0],splittedCommand[1],targets.ToArray());
				}else server.SendMessage(textBox1.Text,targets.ToArray());
				commandHistory.Insert(0,textBox1.Text);
				if(commandHistory.Count>maxHistoryCount) commandHistory.RemoveAt(20);
				textBox1.Text="";
			}
			currentHistory=-1;
			listBox1.SelectedIndex=-1;
			textBox1.Focus();
		}

		void OnNewUserAdded(WebSocketSession session,UserEventArgs e)
		{
			var name=e.UserName;
			var color=colors[new Random(name.GetHashCode()).Next(colors.Length)];
			Invoke((Action)(()=>{
				listBox1.Items.Add(new User(name,color));
				AppendLog(name,name+"さんが接続しました。");
				if(Application.OpenForms.Cast<Form>().Any(f=>f is Form3)) screenShotListView.AddUser(name);
				UpdateTitle();
			}));
		}

		void OnUserRemoved(UserEventArgs e)
		{
			var name=e.UserName;
			Invoke((Action)(()=>{
				if(Application.OpenForms.Cast<Form>().Any(f=>f.Name==name))
					(Application.OpenForms[name] as Form2).Close();
				AppendLog(name,name+"さんが切断しました。");
				listBox1.Items.Remove(listBox1.Items.Cast<User>().First(user=>user.Name==name));
				if(Application.OpenForms.Cast<Form>().Any(f=>f is Form3)) screenShotListView.RemoveUser(name);
				UpdateTitle();
			}));
		}

		void OnMessageReceived(WebSocketSession session,MessageReceivedEventArgs e)
		{
			Invoke((Action)(()=>AppendLog(e.UserName,e.UserName+": "+e.Message)));
		}

		void OnScreenShotReceived(WebSocketSession session,ScreenShotReceivedEventArgs e)
		{
			Invoke((Action)(()=>{
				if(e.Image!=null){
					if(e.Image.Size==thumbnailSize){
						var users=from user in listBox1.Items.Cast<User>() select user.Name;
						screenShotListView.UpdateThumbnail(e.UserName,e.Image);
					}else ShowScreenShotWindow(e.UserName,e.Image);
				}else ShowBalloon("画面の取得に失敗しました。",ToolTipIcon.Error);
			}));
		}

		void ShowScreenShotWindow(string name,Bitmap bitmap)
		{
			if(Application.OpenForms.Cast<Form>().Any(f=>f.Name==name))
				(Application.OpenForms[name] as Form2).UpdateScreenShot(bitmap);
			else new Form2(name,bitmap).Show();
		}

		void contextMenuStrip1_Opening(object sender,CancelEventArgs e)
		{
			toolStripMenuItem1.Enabled=listBox1.SelectedIndex!=-1;
		}

		void toolStripMenuItem1_Click(object sender,EventArgs e)
		{
			var targets=from user in listBox1.SelectedItems.Cast<User>() select user.Name;
			server.SendScreenShotRequest(0,0,targets.ToArray());
		}

		private void toolStripMenuItem2_Click(object sender,EventArgs e)
		{
			if(screenShotListView==null||screenShotListView.IsDisposed){
				var users=from user in listBox1.Items.Cast<User>() select user.Name;
				screenShotListView=new Form3(users.ToArray(),thumbnailSize);
				screenShotListView.Show();
			}else screenShotListView.Focus();
		}

		void Form1_DragEnter(object sender,DragEventArgs e)
		{
			var checkResults=from format in new[]{DataFormats.Text,DataFormats.UnicodeText,DataFormats.OemText,DataFormats.FileDrop}
							 select e.Data.GetDataPresent(format);
			e.Effect=checkResults.Any(result=>result)?DragDropEffects.All:DragDropEffects.None;
		}

		public void RequestScreenShot(string[] users,int width,int height)
		{
			server.SendScreenShotRequest(width,height,users);
		}

		void Form1_DragDrop(object sender,DragEventArgs e)
		{
			if(e.Data.GetDataPresent(typeof(string))){
				textBox1.Text=e.Data.GetData(typeof(string)) as string;
			}else if(e.Data.GetDataPresent(DataFormats.FileDrop)){
				textBox1.Text=(e.Data.GetData(DataFormats.FileDrop) as string[])[0];
			}
			button1_Click(null,null);
		}

		private void listBox1_DoubleClick(object sender,EventArgs e)
		{
			var users=from item in listBox1.SelectedItems.Cast<User>() select item.Name;
			server.SendScreenShotRequest(0,0,users.ToArray());
		}

		void listBox2_DoubleClick(object sender,EventArgs e)
		{
			User user=null;
			foreach(User item in listBox1.Items)
				if(item.Name==(listBox2.SelectedItem as Message).Name)
					user=item;
			if(user==null) return;
			listBox1.SelectedItems.Add(user);
		}

		void AppendLog(string name,string text)
		{
			var color=listBox1.Items.Cast<User>().First(user=>user.Name==name).Color;
			listBox2.Items.Add(new Message(name,text,color));
		}

		void ShowBalloon(string text,ToolTipIcon icon)
		{
			notifyIcon1.ShowBalloonTip(1000,"RemoteController(Server)",text,icon);
		}

		string GetLocalIPAddress()
		{
			var unicastAddresses=from i in NetworkInterface.GetAllNetworkInterfaces()
								 where i.GetIPProperties().GatewayAddresses.Count>0
								 select i.GetIPProperties().UnicastAddresses;
			var localAddresses=new List<byte[]>();
			foreach(var ipList in unicastAddresses)
				foreach(var info in ipList)
					localAddresses.Add(info.Address.GetAddressBytes());
			var localAddressBytes=from bytes in localAddresses
							 where
							 bytes[0]==10||
							 (bytes[0]==172&&bytes[1]>=16&&bytes[1]<=31)||
							 (bytes[0]==192&&bytes[1]==168)
							 select bytes;
			return localAddressBytes.Count()!=0?string.Join(".",localAddressBytes.ToList()[0]):null;
		}

		void GetUserColors()
		{
			var knownColors=from value in Enum.GetValues(typeof(KnownColor)).Cast<KnownColor>()
							select Color.FromKnownColor(value);
			var visibleColors=from color in knownColors
							  where
							  (uint)color.ToArgb()!=0xFF000000&&
							  (uint)color.ToArgb()!=0xFFFFFFFF&&
							  (uint)color.ToArgb()!=0xFF3399FF&&
							  color.GetBrightness()<0.75&&
							  color.GetBrightness()>0.1
							  select color;
			colors=visibleColors.ToArray();
		}

		void UpdateTitle()
		{
			Text="RemoteController(Server)@"+localAddress+"("+listBox1.Items.Count.ToString()+"人接続中)";
		}

		string[] ParseCommand(string text)
		{
			text=text.Trim();
			int index=0;
			var commanddelim=text[0]=='\"'?'\"':' ';
			for(index++;index<text.Length&&text[index]!=commanddelim;index++) continue;
			index-=(commanddelim==' '?1:0);
			var name=text.Substring(0,index+(index==text.Length?0:1));
			for(index++;index<text.Length&&(text[index]==' '||text[index]=='\t');index++) continue;
			string args="";
			if(index<text.Length){
				var str=text.Substring(index,text.Length-index);
				if(!str.All(c=>c==' '||c=='\t')) args=str;
			}
			return new[]{name,args};
		}

		void listBox1_DrawItem(object sender,DrawItemEventArgs e)
		{
			if(e.Index!=-1){
				var user=(User)listBox1.Items[e.Index];
				e.DrawBackground();
				e.Graphics.DrawString(user.Name,e.Font,new SolidBrush(user.Color),e.Bounds);
				e.DrawFocusRectangle();
			}
		}

		void listBox2_DrawItem(object sender,DrawItemEventArgs e)
		{
			if(e.Index!=-1){
				var message=(Message)listBox2.Items[e.Index];
				e.DrawBackground();
				e.Graphics.DrawString(message.Text,e.Font,new SolidBrush(message.Color),e.Bounds);
				e.DrawFocusRectangle();
			}
		}

		private void textBox1_KeyDown(object sender,KeyEventArgs e)
		{
			if(e.KeyCode==Keys.Up){
				if(currentHistory<maxHistoryCount-1&&currentHistory<commandHistory.Count-1) currentHistory++;
				textBox1.Text=commandHistory[currentHistory];
			}else if(e.KeyCode==Keys.Down){
				if(currentHistory>0) currentHistory--;
				textBox1.Text=commandHistory[currentHistory];
			}
		}
	}

	class Message
	{
		public string Name{get;protected set;}
		public string Text{get;protected set;}
		public Color Color{get;protected set;}

		public Message()
		{
			Name="";
			Text="";
			Color=Color.Empty;
		}

		public Message(string name,string text,Color color)
		{
			Name=name;
			Text=text;
			Color=color;
		}
	}

	class User
	{
		public string Name{get;protected set;}
		public Color Color{get;protected set;}

		public User()
		{
			Name="";
			Color=Color.Empty;
		}

		public User(string name,Color color)
		{
			Name=name;
			Color=color;
		}
	}
}
