using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using OpenRa.FileFormats;
using OpenRa.Game.Graphics;
using OpenRa.Game.Orders;

namespace OpenRa.Game
{
	class MainWindow : Form
	{
		readonly Renderer renderer;

		static Size GetResolution(Settings settings)
		{
			var desktopResolution = Screen.PrimaryScreen.Bounds.Size;

			return new Size(
				settings.GetValue("width", desktopResolution.Width),
				settings.GetValue("height", desktopResolution.Height));
		}

		[DllImport("user32")]
		static extern int ShowCursor([MarshalAs(UnmanagedType.Bool)] bool visible);

		public MainWindow(Settings settings)
		{
			FormBorderStyle = FormBorderStyle.None;
			BackColor = Color.Black;
			StartPosition = FormStartPosition.Manual;
			Location = Point.Empty;
			Visible = true;

			UiOverlay.ShowUnitDebug = settings.GetValue("udebug", false);
			UiOverlay.ShowBuildDebug = settings.GetValue("bdebug", false);
			WorldRenderer.ShowUnitPaths = settings.GetValue("pathdebug", false);
			Game.timestep = settings.GetValue("rate", 40);
			Game.Replay = settings.GetValue("replay", "");
			Game.NetworkHost = settings.GetValue("host", "");
			Game.NetworkPort = int.Parse(settings.GetValue("port", "0"));

			var useAftermath = bool.Parse(settings.GetValue("aftermath", "false"));

			Renderer.SheetSize = int.Parse(settings.GetValue("sheetsize", "512"));

			while (!File.Exists("redalert.mix"))
			{
				var current = Directory.GetCurrentDirectory();
				if (Directory.GetDirectoryRoot(current) == current)
					throw new InvalidOperationException("Unable to load MIX files.");
				Directory.SetCurrentDirectory("..");
			}

			FileSystem.MountDefault(useAftermath);

			bool windowed = !settings.GetValue("fullscreen", false);
			renderer = new Renderer(this, GetResolution(settings), windowed);

			var controller = new Controller(() => (Modifiers)(int)ModifierKeys);	/* a bit of insane input routing */

			Game.Initialize(settings.GetValue("map", "scm12ea.ini"), renderer, new int2(ClientSize),
				settings.GetValue("player", 1), useAftermath, controller);

			ShowCursor(false);
			Game.ResetTimer();
		}

		internal void Run()
		{
			while (Created && Visible)
			{
				Game.Tick();
				Application.DoEvents();
			}
		}

		int2 lastPos;

		void DispatchMouseInput(MouseInputEvent ev, MouseEventArgs e)
		{
			Game.viewport.DispatchMouseInput(
				new MouseInput
				{
					Button = (MouseButton)(int)e.Button,
					Event = ev,
					Location = new int2(e.Location),
					Modifiers = (Modifiers)(int)ModifierKeys,
				});
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);
			lastPos = new int2(e.Location);
			DispatchMouseInput(MouseInputEvent.Down, e);
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);

			if (e.Button == MouseButtons.Middle || e.Button == (MouseButtons.Left | MouseButtons.Right))
			{
				int2 p = new int2(e.Location);
				Game.viewport.Scroll(lastPos - p);
				lastPos = p;
			}

			DispatchMouseInput(MouseInputEvent.Move, e);
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			base.OnMouseUp(e);
			DispatchMouseInput(MouseInputEvent.Up, e);
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			base.OnKeyDown(e);

			/* hack hack hack */
			if (e.KeyCode == Keys.F8 && !Game.orderManager.GameStarted)
			{
				Game.LocalPlayer.IsReady ^= true;
				Game.controller.AddOrder(
					new Order( "ToggleReady", Game.LocalPlayer.PlayerActor, null, int2.Zero,
						Game.LocalPlayer.IsReady ? "ready" : "not ready") { IsImmediate = true });
			}

			/* temporary hack: DO NOT LEAVE IN */
			if (e.KeyCode == Keys.F2)
				Game.LocalPlayer = Game.players[(Game.LocalPlayer.Index + 1) % 4];
			if (e.KeyCode == Keys.F3)
				Game.controller.orderGenerator = new SellOrderGenerator();

			if (!Game.chat.isChatting)
				if (e.KeyCode >= Keys.D0 && e.KeyCode <= Keys.D9)
					Game.controller.DoControlGroup( (int)e.KeyCode - (int)Keys.D0, (Modifiers)(int)e.Modifiers );
		}

		protected override void OnKeyPress(KeyPressEventArgs e)
		{
			base.OnKeyPress(e);

			if (e.KeyChar == '\r')
				Game.chat.Toggle();
			else if (Game.chat.isChatting)
				Game.chat.TypeChar(e.KeyChar);
		}
	}

	[Flags]
	enum MouseButton
	{
		None = (int)MouseButtons.None,
		Left = (int)MouseButtons.Left,
		Right = (int)MouseButtons.Right,
		Middle = (int)MouseButtons.Middle,
	}

	[Flags]
	enum Modifiers
	{
		None = (int)Keys.None,
		Shift = (int)Keys.Shift,
		Alt = (int)Keys.Alt,
		Ctrl = (int)Keys.Control,
	}

	struct MouseInput
	{
		public MouseInputEvent Event;
		public int2 Location;
		public MouseButton Button;
		public Modifiers Modifiers;
		public bool IsFake;
	}

	enum MouseInputEvent { Down, Move, Up };
}
