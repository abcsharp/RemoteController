using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Drawing.Imaging;
using System.IO;
using WebSocket4Net;
using SuperSocket.ClientEngine;
using System.Diagnostics;

namespace Client
{
	public partial class Form1 : Form
	{
		WebSocket socket;
		const int port=7345;
		const string delimiter="----------\n\n----------";
		EncoderParameters encoderParams;
		ImageCodecInfo jpegCodec;
		bool connected;

		public Form1()
		{
			InitializeComponent();
			connected=false;
			encoderParams=new EncoderParameters();
			encoderParams.Param[0]=new EncoderParameter(System.Drawing.Imaging.Encoder.Quality,95L);
			jpegCodec=ImageCodecInfo.GetImageEncoders().First(codec=>codec.MimeType=="image/jpeg");
		}

		void Form1_FormClosing(object sender,FormClosingEventArgs e)
		{
			if(socket!=null){
				socket.Closed-=OnClosed;
				if(socket.State==WebSocketState.Open) socket.Close();
			}
		}

		Bitmap CreateThumbnail(Bitmap original,int width,int height)
		{
			var bmp=new Bitmap(width,height);
			var graphics=Graphics.FromImage(bmp);
			graphics.Clear(Color.White);
			float drawWidth=original.Width,drawHeight=original.Height;
			if(original.Width>width||original.Height>height){
				var scale=Math.Min((float)width/original.Width,(float)height/original.Height);
				drawWidth*=scale;
				drawHeight*=scale;
			}
			graphics.DrawImage(original,(width-drawWidth)/2,(height-drawHeight)/2,drawWidth,drawHeight);
			return bmp;
		}

		void SendScreenShot(int width,int height)
		{
			var size=SystemInformation.VirtualScreen.Size;
			var fileData="";
			using(var bitmap=new Bitmap(size.Width,size.Height,PixelFormat.Format24bppRgb))
			using(var graphics=Graphics.FromImage(bitmap))
			using(var stream=new MemoryStream(1048576)){
				graphics.CopyFromScreen(Point.Empty,Point.Empty,size);
				if(width!=0&&height!=0) 
					using(var smallBitmap=CreateThumbnail(bitmap,width,height))
						smallBitmap.Save(stream,jpegCodec,encoderParams);
				else bitmap.Save(stream,jpegCodec,encoderParams);
				fileData=Convert.ToBase64String(stream.ToArray());
			}
			try{
				socket.Send(CreateMessage(new[]{"screenshotres",fileData}));
			}catch(Exception){}
		}

		void OnMesageReceived(object sender,MessageReceivedEventArgs args)
		{
			var lines=ParseMessage(args.Message);
			Invoke((Action)(()=>ProcessMessage(lines[0],lines.Skip(1).ToArray())));
		}

		void OnOpened(object sender,EventArgs e)
		{
			socket.Send(CreateMessage(new[]{"join",textBox1.Text}));
		}

		void OnClosed(object sender,EventArgs e)
		{
			if(connected){
				ShowBalloon("接続を切断しました。",ToolTipIcon.Info);
				Invoke((Action)DisConnect);
			}
		}

		void OnError(object sender,SuperSocket.ClientEngine.ErrorEventArgs e)
		{
			if(socket.State==WebSocketState.Connecting) ShowBalloon("接続に失敗しました。",ToolTipIcon.Error);
			else if(socket.State==WebSocketState.Open) ShowBalloon("データの送信に失敗しました。",ToolTipIcon.Error);
			else ShowBalloon("原因不明のエラーが発生しました。",ToolTipIcon.Error);
			Invoke((Action)DisConnect);
		}

		void CreateWebSocket(string ip)
		{
			socket=new WebSocket("ws://"+ip+":"+port.ToString(),null,WebSocketVersion.Rfc6455);
			socket.MessageReceived+=OnMesageReceived;
			socket.Opened+=OnOpened;
			socket.Closed+=OnClosed;
			socket.Error+=OnError;
		}

		void button1_Click(object sender,EventArgs e)
		{
			if(textBox1.Text!=""&&maskedTextBox1.MaskCompleted){
				if(socket==null||socket.State!=WebSocketState.Open) Connect();
				else socket.Close();
			}
		}

		void Connect()
		{
			textBox1.Enabled=false;
			maskedTextBox1.Enabled=false;
			button1.Enabled=false;
			CreateWebSocket(maskedTextBox1.Text.Replace(" ",""));
			socket.Open();
		}

		void DisConnect()
		{
			Text="RemoteController(Client)";
			textBox1.Enabled=true;
			maskedTextBox1.Enabled=true;
			button1.Enabled=true;
			textBox2.Enabled=false;
			button2.Enabled=false;
			button1.Text="接続";
			AcceptButton=button1;
			connected=false;
			if(socket!=null&&socket.State==WebSocketState.Open) socket.Close();
		}

		void button2_Click(object sender,EventArgs e)
		{
			socket.Send(CreateMessage(new[]{"message",textBox2.Text}));
			textBox2.Text="";
			textBox2.Focus();
		}

		void ProcessMessage(string type,string[] args)
		{
			switch(type){
			case "joinres":
				if(args.Length>1){
					var result=bool.Parse(args[0]);
					ShowBalloon(args[1],result?ToolTipIcon.Info:ToolTipIcon.Error);
					if(result){
						Text="RemoteController(Client)@"+textBox1.Text;
						button1.Enabled=true;
						button1.Text="切断";
						textBox2.Enabled=true;
						button2.Enabled=true;
						AcceptButton=button2;
						connected=true;
					}else DisConnect();
				}else{
					ShowBalloon("不正なレスポンスが返されました。",ToolTipIcon.Error);
					DisConnect();
				}
				break;
			case "open":
			case "execute":
				if(args.Length>0)
					try{
						Process.Start(args[0],args.Length>1?args[1]:"");
					}catch(Exception){}
				break;
			case "message":
				if(args.Length>0) ShowBalloon(args[0],ToolTipIcon.Info);
				break;
			case "screenshot":
				if(args.Length>1) ThreadPool.QueueUserWorkItem((_)=>SendScreenShot(int.Parse(args[0]),int.Parse(args[1])));
				break;
			default:
				break;
			}
			GC.Collect();
		}

		void Form1_Load(object sender,EventArgs e)
		{
			var args=Environment.GetCommandLineArgs();
			if(args.Length>1){
				maskedTextBox1.Text=string.Format("{0,3}.{1,3}.{2,3}.{3,3}",args[1].Split(new[]{'.'}));
				if(args.Length>2){
					textBox1.Text=args[2];
					button1_Click(null,null);
				}
			}
		}

		string[] ParseMessage(string message)
		{
			return message.Split(new[]{delimiter},StringSplitOptions.RemoveEmptyEntries);
		}

		string CreateMessage(string[] lines)
		{
			return string.Join(delimiter,lines);
		}

		void ShowBalloon(string text,ToolTipIcon icon)
		{
			notifyIcon1.ShowBalloonTip(1000,"RemoteController(Client)",text,icon);
		}
	}
}
