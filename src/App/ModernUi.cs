using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
namespace Gerenciador;
sealed partial class MainForm{
    Panel? workspace,nav;
    readonly List<Button> navButtons=new();
    string selectedSection="home";
    // The vector paths are original line drawings, rendered locally, not downloaded.
    static string ButtonIcon(string text){
        var t=text.Replace("&","").ToLowerInvariant();
        if(t.Contains("início"))return "home";
        if(t.Contains("feedback")||t.Contains("sugerir")||t.Contains("relatar"))return "chat";
        if(t.Contains("backup")||t.Contains("recuper"))return "archive";
        if(t.Contains("diagn"))return "pulse";
        if(t.Contains("gerenciar mods"))return "puzzle";
        if(t.Contains("prefer")||t.Contains("opções")||t.Contains("configur"))return "settings";
        if(t.Contains("atualiz")||t.Contains("reinstal"))return "refresh";
        if(t.Contains("registro")||t.Contains("relatório"))return "log";
        if(t.Contains("preparar")||t.Contains("executar")||t.Contains("analisar"))return "check";
        if(t.Contains("procurar")||t.Contains("pasta")||t.Contains("localizar")||t.Contains("detectar")||t.Contains("pesquis"))return "search";
        if(t.Contains("adicionar"))return "plus";
        if(t.Contains("avançad")||t.Contains("lua"))return "sliders";
        if(t.Contains("mod"))return "puzzle";
        if(t.Contains("fechar")||t.Contains("cancelar"))return "close";
        if(t.Contains("revis")||t.Contains("confirm")||t.Contains("salvar")||t.Contains("usar seleção"))return "check";
        if(t.Contains("site")||t.Contains("como usar")||t.Contains("guia"))return "globe";
        return "";
    }
    static void Glyph(Graphics g,string kind,RectangleF bounds,Color color){
        if(kind.Length==0)return;
        var state=g.Save();
        g.SmoothingMode=SmoothingMode.AntiAlias;g.TranslateTransform(bounds.X,bounds.Y);g.ScaleTransform(bounds.Width/24,bounds.Height/24);
        using var p=new Pen(color,1.7f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round};
        void Line(params float[] a){var pts=Enumerable.Range(0,a.Length/2).Select(i=>new PointF(a[2*i],a[2*i+1])).ToArray();g.DrawLines(p,pts);}
        void R(float x,float y,float w,float h)=>g.DrawRectangle(p,x,y,w,h);
        void E(float x,float y,float w,float h)=>g.DrawEllipse(p,x,y,w,h);
        switch(kind){
            case "home":Line(3,11,12,3,21,11);Line(6,10,6,21,10,21,10,15,14,15,14,21,18,21,18,10);break;
            case "server":R(3,4,18,7);R(3,13,18,7);Line(6,7,8,7);Line(6,16,8,16);break;
            case "search":E(3,3,13,13);Line(13,13,21,21);break;
            case "archive":R(4,8,16,13);R(3,3,18,5);Line(9,12,15,12);break;
            case "pulse":Line(2,12,7,12,10,5,14,19,17,12,22,12);break;
            case "refresh":g.DrawArc(p,4,4,16,16,210,285);Line(20,3,20,9,14,9);break;
            case "puzzle":Line(4,4,10,4,10,8,14,8,14,4,20,4,20,10,16,10,16,14,20,14,20,20,14,20,14,16,10,16,10,20,4,20,4,14,8,14,8,10,4,10,4,4);break;
            case "log":Line(5,3,15,3,19,7,19,21,5,21,5,3);Line(14,3,14,8,19,8);Line(8,12,16,12);Line(8,16,16,16);break;
            case "settings":E(5,5,14,14);E(9,9,6,6);for(int i=0;i<8;i++){double a=i*Math.PI/4;Line(12+8*(float)Math.Cos(a),12+8*(float)Math.Sin(a),12+10*(float)Math.Cos(a),12+10*(float)Math.Sin(a));}break;
            case "sliders":Line(3,6,21,6);Line(3,12,21,12);Line(3,18,21,18);g.DrawLine(p,8,3,8,9);g.DrawLine(p,16,9,16,15);g.DrawLine(p,10,15,10,21);break;
            case "chat":Line(3,3,21,3,21,17,10,17,4,21,4,17,3,17,3,3);Line(7,8,17,8);Line(7,12,14,12);break;
            case "check":Line(4,12,10,18,21,6);break;
            case "close":Line(5,5,19,19);Line(19,5,5,19);break;
            case "plus":Line(12,4,12,20);Line(4,12,20,12);break;
            case "globe":E(3,3,18,18);E(8,3,8,18);Line(3,12,21,12);break;
            case "info":E(3,3,18,18);Line(12,11,12,17);Line(12,7,12,7.5f);break;
            case "warning":Line(12,3,22,20,2,20,12,3);Line(12,9,12,14);Line(12,17,12,17.5f);break;
        }
        g.Restore(state);
    }
    static GraphicsPath Rounded(Rectangle r,int radius){
        var path=new GraphicsPath();int d=Math.Min(radius*2,Math.Min(r.Width,r.Height));
        if(d<=0){path.AddRectangle(r);return path;}
        path.AddArc(r.X,r.Y,d,d,180,90);path.AddArc(r.Right-d,r.Y,d,d,270,90);path.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);path.AddArc(r.X,r.Bottom-d,d,d,90,90);path.CloseFigure();return path;
    }
    static void DecorateButton(Button b,bool card=false,string? icon=null,bool compact=false){
        if(SystemInformation.HighContrast)return;
        var kind=icon??ButtonIcon(b.Text);
        // All buttons receive consistent disabled and focus treatment.
        bool hover=false,down=false;
        float blend=0;
        var motion=new System.Windows.Forms.Timer{Interval=16};
        Font? titleFont=null,descriptionFont=null;
        b.FontChanged+=(_,_)=>{titleFont?.Dispose();descriptionFont?.Dispose();titleFont=descriptionFont=null;};
        void Animate(){
            if(!SystemInformation.UIEffectsEnabled||!b.Enabled||!b.Visible){motion.Stop();blend=hover&&b.Enabled?1:0;b.Invalidate();return;}
            motion.Start();
        }
        motion.Tick+=(_,_)=>{
            float target=hover&&b.Enabled?1:0;
            blend=Math.Abs(target-blend)<=1f/8?target:blend+Math.Sign(target-blend)/8f;
            b.Invalidate();if(blend==target)motion.Stop();
        };
        b.Disposed+=(_,_)=>{motion.Dispose();titleFont?.Dispose();descriptionFont?.Dispose();};
        b.MouseEnter+=(_,_)=>{hover=true;Animate();};b.MouseLeave+=(_,_)=>{hover=false;down=false;Animate();};
        b.VisibleChanged+=(_,_)=>{if(!b.Visible){motion.Stop();hover=down=false;blend=0;}};b.MouseDown+=(_,_)=>{down=true;b.Invalidate();};b.MouseUp+=(_,_)=>{down=false;b.Invalidate();};
        b.GotFocus+=(_,_)=>b.Invalidate();b.LostFocus+=(_,_)=>b.Invalidate();b.EnabledChanged+=(_,_)=>{Animate();b.Invalidate();};
        b.Paint+=(_,e)=>{
            var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            int scale(int v)=>Math.Max(1,b.LogicalToDeviceUnits(v));
            var r=new Rectangle(1,1,b.Width-3,b.Height-3);if(r.Width<4||r.Height<4)return;
            Color Mix(Color a,Color c,float t)=>Color.FromArgb((int)(a.R+(c.R-a.R)*t),(int)(a.G+(c.G-a.G)*t),(int)(a.B+(c.B-a.B)*t));
            var fill=!b.Enabled?PanelColor:down?SelectionColor:b.BackColor==Accent?Accent:Mix(b.BackColor,Raised,blend);
            var fg=b.Enabled?b.ForeColor:Muted;
            using var background=new SolidBrush(b.Parent?.BackColor??Bg);g.FillRectangle(background,b.ClientRectangle);
            using var shape=Rounded(r,scale(card?10:6));using var brush=new SolidBrush(fill);g.FillPath(brush,shape);
            using var edge=new Pen(b.Focused?Accent:Border,b.Focused?2:1);g.DrawPath(edge,shape);
            var text=b.Text.Replace("&","");
            int iconSize=scale(card?28:17);
            if(card){
                var lines=text.Split('\n',2);int x=scale(16),y=scale(12);
                Glyph(g,kind,new RectangleF(x,y,iconSize,iconSize),b.Enabled?Accent:Muted);
                var title=titleFont??=new Font(b.Font.FontFamily,13,FontStyle.Bold);
                var desc=descriptionFont??=new Font(b.Font.FontFamily,10);
                int ty=y+iconSize+scale(5);
                if(b.Height<scale(128)){ty=scale(10);x=scale(16)+iconSize+scale(10);}
                TextRenderer.DrawText(g,lines[0],title,new Rectangle(x,ty,Math.Max(1,r.Width-2*x),scale(24)),fg,TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix);
                if(lines.Length>1)TextRenderer.DrawText(g,lines[1],desc,new Rectangle(x,ty+scale(25),Math.Max(1,r.Width-2*x),Math.Max(1,r.Height-ty-scale(25))),Muted,TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);
            }else{
                int x=scale(9),y=(b.Height-iconSize)/2;
                bool showIcon=kind.Length>0&&(b.Width>=TextRenderer.MeasureText(text,b.Font).Width+iconSize+scale(22)||compact);
                if(showIcon)Glyph(g,kind,new RectangleF(x,y,iconSize,iconSize),fg);
                if(!compact||b.Width>scale(70)){
                    int left=showIcon?x+iconSize+scale(7):scale(7);
                    TextRenderer.DrawText(g,b.Text,b.Font,new Rectangle(left,0,Math.Max(1,b.Width-left-scale(6)),b.Height),fg,TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|(b.Focused?TextFormatFlags.Default:TextFormatFlags.HidePrefix));
                }
            }
        };
    }
    void BuildShell(){
        workspace=new BufferedPanel{Dock=DockStyle.Fill,BackColor=Bg};
        Controls.Remove(mainHost!);Controls.Remove(homeHost!);
        workspace.Controls.Add(mainHost!);workspace.Controls.Add(homeHost!);
        nav=new BufferedPanel{Dock=DockStyle.Left,Width=LogicalToDeviceUnits(186),BackColor=PanelColor,AutoScroll=true,Padding=new Padding(6)};
        var stack=new FlowLayoutPanel{Dock=DockStyle.Top,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,BackColor=PanelColor};
        nav.Controls.Add(stack);
        void Add(string name,string glyph,string section,Action action){
            var b=new Button{Text=name,AccessibleName=name,AccessibleDescription="Navegação: "+name,Height=LogicalToDeviceUnits(37),Width=LogicalToDeviceUnits(168),AutoSize=false,Margin=new Padding(2,2,2,2),TextAlign=ContentAlignment.MiddleLeft,Tag=section,FlatStyle=FlatStyle.Flat,BackColor=PanelColor,ForeColor=Ink};
            b.FlatAppearance.BorderColor=Border;
            DecorateButton(b,false,glyph,true);b.Click+=(_,_)=>{if(busy)return;action();};
            stack.Controls.Add(b);navButtons.Add(b);hints.SetToolTip(b,name);
        }
        Add("Início","home","home",ShowHome);
        Add("Ambiente","server","environment",()=>OpenTask(2,false,section:"environment"));
        Add("Mods","puzzle","mods",()=>OpenTask(2,false));
        Add("Atualização","refresh","update",()=>OpenTask(2,false,()=>UpdateDedicated(),true,"update"));
        Add("Diagnóstico","pulse","diagnostic",()=>OpenTask(2,false,()=>Diagnose(),section:"diagnostic"));
        Add("Backups","archive","backups",()=>OpenTask(2,false,()=>OpenBackups(),section:"backups"));
        Add("Registro","log","log",ShowSessionLog);
        Add("Avançado","sliders","advanced",()=>OpenTask(2,true));
        Add("Preferências","settings","preferences",ShowPreferences);
        Add("Feedback","chat","feedback",ShowFeedback);
        Controls.Add(workspace);Controls.Add(nav);
        void LayoutNav(){
            int width=ClientSize.Width>=LogicalToDeviceUnits(1050)||SystemInformation.HighContrast?LogicalToDeviceUnits(186):LogicalToDeviceUnits(52);
            nav.Width=width;
            foreach(var b in navButtons)b.Width=Math.Max(20,width-LogicalToDeviceUnits(16));
        }
        SizeChanged+=(_,_)=>LayoutNav();HandleCreated+=(_,_)=>ApplyCaptionTheme();if(IsHandleCreated)ApplyCaptionTheme();LayoutNav();SelectNav("home");
    }
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
    void ApplyCaptionTheme(){
        if(SystemInformation.HighContrast)return;
        try{
            int dark=Bg.GetBrightness()<0.5f?1:0;
            if(DwmSetWindowAttribute(Handle,20,ref dark,4)!=0)DwmSetWindowAttribute(Handle,19,ref dark,4);
        }catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
    }
    void SelectNav(string section){
        selectedSection=section;
        if(workspaceHeading!=null)workspaceHeading.Text=section switch{"environment"=>"Ambiente do servidor","update"=>"Atualização do dedicado","diagnostic"=>"Diagnóstico do servidor","backups"=>"Central de backups","advanced"=>"Ferramentas avançadas",_=>"Gerenciar mods"};
        foreach(var b in navButtons){b.BackColor=(string)b.Tag! == section?Raised:PanelColor;b.ForeColor=(string)b.Tag! == section?Accent:Ink;b.Invalidate();}
    }
    static void ModernControls(Control root){
        foreach(Control c in root.Controls){
            if(c is ComboBox combo&&!SystemInformation.HighContrast){
                combo.FlatStyle=FlatStyle.Flat;combo.BackColor=PanelColor;combo.ForeColor=Ink;
                if(combo.DrawMode==DrawMode.Normal){
                    combo.DrawMode=DrawMode.OwnerDrawFixed;
                    combo.DrawItem+=(_,e)=>{
                        if(e.Bounds.Width<=0||e.Bounds.Height<=0)return;
                        bool selected=(e.State&DrawItemState.Selected)!=0;
                        using var bg=new SolidBrush(selected?SelectionColor:PanelColor);e.Graphics.FillRectangle(bg,e.Bounds);
                        var value=e.Index>=0&&e.Index<combo.Items.Count?combo.GetItemText(combo.Items[e.Index]):combo.Items.Count==0?"Nenhum resultado encontrado":"Selecione...";
                        TextRenderer.DrawText(e.Graphics,value,e.Font??combo.Font,e.Bounds,combo.Enabled?Ink:Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
                        e.DrawFocusRectangle();
                    };
                }
            }
            ModernControls(c);
        }
    }
    // Dialogs preserve native modal ownership, keyboard semantics and return values.
    sealed class ModernDialog:Form{
        protected override void OnLoad(EventArgs e){
            if(!SystemInformation.HighContrast){
                FormBorderStyle=FormBorderStyle.None;Padding=new Padding(12,8,12,12);
                var bar=new Panel{Dock=DockStyle.Top,Height=LogicalToDeviceUnits(45),BackColor=PanelColor};
                var title=new Label{Text=Text,Dock=DockStyle.Fill,Font=new Font("Segoe UI Semibold",12),ForeColor=Ink,TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true,Padding=new Padding(9,0,0,0)};
                var close=new Button{Text="×",AccessibleName="Fechar janela",Dock=DockStyle.Right,Width=LogicalToDeviceUnits(46),FlatStyle=FlatStyle.Flat,BackColor=PanelColor,ForeColor=Ink,DialogResult=DialogResult.Cancel};
                close.FlatAppearance.BorderColor=Border;DecorateButton(close,false,"close",true);
                close.Click+=(_,_)=>{DialogResult=DialogResult.Cancel;Close();};
                bar.Controls.Add(title);bar.Controls.Add(close);
                Controls.Add(bar);bar.SendToBack();
                int delta=LogicalToDeviceUnits(30);
                var work=Screen.FromControl(Owner??this).WorkingArea;
                Size=new Size(Math.Min(Width,work.Width),Math.Min(Height+delta,work.Height));
                title.MouseDown+=(_,a)=>{if(a.Button==MouseButtons.Left){ReleaseCapture();SendMessage(Handle,0xA1,(IntPtr)2,IntPtr.Zero);}};
                if(Owner!=null)Location=new Point(Math.Clamp(Owner.Left+(Owner.Width-Width)/2,work.Left,Math.Max(work.Left,work.Right-Width)),Math.Clamp(Owner.Top+(Owner.Height-Height)/2,work.Top,Math.Max(work.Top,work.Bottom-Height)));
                ModernControls(this);
            }
            base.OnLoad(e);
        }
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h,int m,IntPtr w,IntPtr l);
    }
    // A real indeterminate state; no invented percentages. Animation respects Windows effects.
    sealed class ActivityIndicator:Control{
        readonly System.Windows.Forms.Timer clock=new(){Interval=100};
        int frame;
        public ActivityIndicator(){
            SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.UserPaint,true);
            AccessibleName="Operação em andamento";AccessibleRole=AccessibleRole.ProgressBar;
            clock.Tick+=(_,_)=>{frame=(frame+1)%20;Invalidate();};
            VisibleChanged+=(_,_)=>{clock.Enabled=Visible&&SystemInformation.UIEffectsEnabled&&!SystemInformation.HighContrast;Invalidate();};
        }
        protected override void OnPaint(PaintEventArgs e){
            e.Graphics.Clear(Parent?.BackColor??Bg);
            var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            var r=new Rectangle(3,Math.Max(2,(Height-8)/2),Math.Max(1,Width-6),8);
            using var path=Rounded(r,4);using var track=new SolidBrush(PanelColor);g.FillPath(track,path);
            using var edge=new Pen(Border);g.DrawPath(edge,path);
            var state=g.Save();g.SetClip(path);
            using var brush=new SolidBrush(Accent);int segment=Math.Max(12,r.Width/3);
            int x=r.X+frame*(r.Width+segment)/19-segment;
            if(!clock.Enabled)x=r.X+r.Width/3;
            g.FillRectangle(brush,x,r.Y,segment,r.Height);g.Restore(state);
        }
        protected override void Dispose(bool disposing){if(disposing)clock.Dispose();base.Dispose(disposing);}
    }
    static class Notice{
        public static DialogResult Show(string message,string title="Gerenciador de Dedicado",MessageBoxButtons buttons=MessageBoxButtons.OK,MessageBoxIcon icon=MessageBoxIcon.Information,MessageBoxDefaultButton defaultButton=MessageBoxDefaultButton.Button1)=>Show(Form.ActiveForm,message,title,buttons,icon,defaultButton);
        public static DialogResult Show(IWin32Window? owner,string message,string title="Gerenciador de Dedicado",MessageBoxButtons buttons=MessageBoxButtons.OK,MessageBoxIcon icon=MessageBoxIcon.Information,MessageBoxDefaultButton defaultButton=MessageBoxDefaultButton.Button1){
            if(SystemInformation.HighContrast)return MessageBox.Show(owner,message,title,buttons,icon,defaultButton);
            using var dialog=new ModernDialog{Text=title,Size=new Size(590,message.Length>300?390:275),MinimumSize=new Size(450,250),StartPosition=FormStartPosition.CenterParent,BackColor=Bg,ForeColor=Ink,Font=new Font("Segoe UI",11),ShowInTaskbar=false};
            var body=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=message.Length>300?ScrollBars.Vertical:ScrollBars.None,WordWrap=true,BorderStyle=BorderStyle.None,BackColor=PanelColor,ForeColor=Ink,Text=message.Replace("\r\n","\n").Replace("\n",Environment.NewLine),AccessibleName=title+" — mensagem",TabStop=true};
            var header=new Label{Dock=DockStyle.Top,Height=48,Text=icon==MessageBoxIcon.Warning?"Atenção":title=="Atualizações"?"Status da atualização":"Informação",Font=new Font("Segoe UI Semibold",15),ForeColor=icon==MessageBoxIcon.Warning?WarningColor:Accent,TextAlign=ContentAlignment.MiddleLeft};
            var row=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=55,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(0,7,0,0)};
            var choices=buttons switch{MessageBoxButtons.YesNo=>new[]{("Sim",DialogResult.Yes),("Não",DialogResult.No)},MessageBoxButtons.YesNoCancel=>new[]{("Sim",DialogResult.Yes),("Não",DialogResult.No),("Cancelar",DialogResult.Cancel)},_=>new[]{("Fechar",DialogResult.OK)}};
            var created=new List<Button>();
            foreach(var (label,result) in choices){
                var caption=title=="Atualizações"?(result==DialogResult.Yes?"Abrir site":"Fechar"):label;
                var button=new Button{Text=caption,DialogResult=result};Style(button);created.Add(button);
            }
            foreach(var b in created.AsEnumerable().Reverse())row.Controls.Add(b);
            var defaultIndex=Math.Clamp((int)defaultButton/256,0,created.Count-1);
            dialog.AcceptButton=created[defaultIndex];dialog.CancelButton=created[^1];
            dialog.Controls.Add(body);dialog.Controls.Add(header);dialog.Controls.Add(row);
            dialog.Shown+=(_,_)=>created[defaultIndex].Focus();
            var resultValue=owner==null?dialog.ShowDialog():dialog.ShowDialog(owner);
            return resultValue==DialogResult.Cancel&&buttons==MessageBoxButtons.YesNo?DialogResult.No:resultValue;
        }
    }
}