using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using SuperWebSocket;
using System.Threading;
using SuperSocket.SocketBase;
using SuperSocket.SocketBase.Config;
using System.Drawing;
using System.IO;

namespace Server
{
	class UserEventArgs:EventArgs
	{
		public string UserName{get;private set;}

		public UserEventArgs(string userName)
		{
			UserName=userName;
		}
	}

	class MessageReceivedEventArgs:EventArgs
	{
		public string UserName{get;private set;}
		public string Message{get;private set;}
		
		public MessageReceivedEventArgs(string userName,string message)
		{
			UserName=userName;
			Message=message;
		}
	}

	class ScreenShotReceivedEventArgs:EventArgs
	{
		public string UserName{get;private set;}
		public Bitmap Image{get;private set;}

		public ScreenShotReceivedEventArgs(string userName,byte[] fileData)
		{
			UserName=userName;
			if(fileData==null) Image=null;
			else using(var stream=new MemoryStream(fileData)) Image=new Bitmap(stream);
		}
	}

	class MessageServer
	{
		WebSocketServer server;
		const int port=7345;
		const string delimiter="----------\n\n----------";
		Dictionary<string,WebSocketSession> sessions;

		public event Action<WebSocketSession,UserEventArgs> NewUserAdded;
		public event Action<UserEventArgs> UserRemoved;
		public event Action<WebSocketSession,MessageReceivedEventArgs> MessageReceived;
		public event Action<WebSocketSession,ScreenShotReceivedEventArgs> ScreenShotReceived;

		public MessageServer()
		{
			server=new WebSocketServer();
			sessions=new Dictionary<string,WebSocketSession>();
			InitServer();
		}

		void InitServer()
		{
			server=new WebSocketServer();
			var config=new ServerConfig(){
				Ip="Any",
				Port=port,
				KeepAliveInterval=15,
				MaxRequestLength=1048576
			};
			server.NewMessageReceived+=OnNewMessageReceived;
			server.SessionClosed+=OnSessionClosed;
			server.Setup(config);
		}

		public void Start()
		{
			server.Start();
		}

		public void Stop()
		{
			server.Stop();
		}

		byte[] ConvertData(string base64String)
		{
			byte[] fileData=null;
			try{
				fileData=Convert.FromBase64String(base64String);
			}catch(Exception){
				return null;
			}
			return fileData;
		}

		dynamic CheckJoinRequset(WebSocketSession session,string[] args)
		{
			if(!sessions.Any(pair=>pair.Value==session)){
				if(args.Length>0){
					if(!sessions.Any(pair=>pair.Key==args[0]))
						return new{IsSucceeded=true,Message="接続に成功しました。"};
					else return new{IsSucceeded=false,Message="その名前は既に使われています。"};
				}else return new{IsSucceeded=false,Message="不正な接続要求です。"};
			}else return new{IsSucceeded=false,Message="既に接続しています。"};
		}

		void ProcessMessage(WebSocketSession session,string type,string[] args)
		{
			switch(type){
			case "join":
				var result=CheckJoinRequset(session,args);
				session.Send(CreateMessage(new[]{"joinres",(string)result.IsSucceeded.ToString(),(string)result.Message}));
				if(result.IsSucceeded){
					if(NewUserAdded!=null){
						sessions.Add(args[0],session);
						NewUserAdded(session,new UserEventArgs(args[0]));
					}
				}else session.Close();
				break;
			case "message":
				var name=sessions.FirstOrDefault(pair=>pair.Value==session).Key;
				if(name!=""&&args.Length>0&&MessageReceived!=null)
					MessageReceived(session,new MessageReceivedEventArgs(name,args[0]));
				break;
			case "screenshotres":
				name=sessions.FirstOrDefault(pair=>pair.Value==session).Key;
				if(name!=""&&args.Length>0){
					ThreadPool.QueueUserWorkItem((arg)=>{
						var data=ConvertData(arg as string);
						if(ScreenShotReceived!=null)
							ScreenShotReceived(session,new ScreenShotReceivedEventArgs(name,data));
					},args[0]);
				}
				break;
			default:
				break;
			}
		}

		void OnNewMessageReceived(WebSocketSession session,string message)
		{
			var lines=ParseMessage(message);
			ProcessMessage(session,lines[0],lines.Skip(1).ToArray());
		}

		void OnSessionClosed(WebSocketSession session,SuperSocket.SocketBase.CloseReason reason)
		{
			var pair=sessions.FirstOrDefault(p=>p.Value==session);
			if(pair.Key!=null){
				sessions.Remove(pair.Key);
				if(UserRemoved!=null) UserRemoved(new UserEventArgs(pair.Key));
			}
		}

		void Send(string message,string[] targets)
		{
			foreach(var target in targets) sessions[target].Send(message);
		}

		public void SendOpenRequest(string resourceName,string[] targets)
		{
			var message=CreateMessage(new[]{"open",resourceName});
			Send(message,targets);
		}

		public void SendExecuteRequest(string commandName,string commandArgs,string[] targets)
		{
			var message=CreateMessage(new[]{"execute",commandName,commandArgs});
			Send(message,targets);
		}

		public void SendScreenShotRequest(int width,int height,string[] targets)
		{
			var message=CreateMessage(new[]{"screenshot",width.ToString(),height.ToString()});
			Send(message,targets);
		}

		public void SendMessage(string text,string[] targets)
		{
			var message=CreateMessage(new[]{"message",text});
			Send(message,targets);
		}

		string[] ParseMessage(string message)
		{
			return message.Split(new[]{delimiter},StringSplitOptions.RemoveEmptyEntries);
		}

		string CreateMessage(string[] lines)
		{
			return string.Join(delimiter,lines);
		}
	}

	static class Program
	{
		/// <summary>
		/// アプリケーションのメイン エントリ ポイントです。
		/// </summary>
		[STAThread]
		static void Main()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.Run(new Form1());
		}
	}
}
