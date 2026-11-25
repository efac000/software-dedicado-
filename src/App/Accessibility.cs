namespace Gerenciador;
sealed partial class MainForm{
    Panel? mainHost,homeHost;
    const string FeedbackBase="https://github.com/efac000/software-dedicado-/issues";
    Panel CreateViewport(Control content,int minWidth,int minHeight){
        var viewport=new BufferedPanel{Dock=DockStyle.Fill,AutoScroll=true,BackColor=Bg,TabStop=false};
        content.Dock=DockStyle.None;content.Location=Point.Empty;
        viewport.Controls.Add(content);
        bool queued=false;
        void Resize(){
            ResizeViewport(viewport,content,minWidth,minHeight);
            if(queued||!viewport.IsHandleCreated||viewport.IsDisposed)return;
            queued=true;
            viewport.BeginInvoke(new Action(()=>{
                queued=false;if(viewport.IsDisposed)return;
                ResizeViewport(viewport,content,minWidth,minHeight);
                viewport.PerformLayout();
            }));
        }
        viewport.SizeChanged+=(_,_)=>Resize();
        viewport.HandleCreated+=(_,_)=>Resize();
        return viewport;
    }
    internal static Size CalculateViewport(Size size,int w,int h,int barW,int barH){
        bool vertical=size.Height<h,horizontal=size.Width<w;
        vertical|=horizontal&&size.Height-barH<h;
        horizontal|=vertical&&size.Width-barW<w;
        return new Size(Math.Max(w,size.Width-(vertical?barW:0)),Math.Max(h,size.Height-(horizontal?barH:0)));
    }
    void ResizeViewport(Panel viewport,Control content,int minWidth,int minHeight){
        if(viewport.ClientSize.Width<=0||viewport.ClientSize.Height<=0)return;
        int w=LogicalToDeviceUnits(minWidth),h=LogicalToDeviceUnits(minHeight);
        var size=viewport.Size;
        int barW=SystemInformation.VerticalScrollBarWidth,barH=SystemInformation.HorizontalScrollBarHeight;
        var desired=CalculateViewport(size,w,h,barW,barH);
        if(content.Size!=desired)content.Size=desired;
        viewport.AutoScrollMinSize=new Size(w,h);
    }
    void FitWorkingArea(){
        var work=Screen.FromControl(this).WorkingArea;
        MinimumSize=new Size(Math.Min(LogicalToDeviceUnits(620),work.Width),Math.Min(LogicalToDeviceUnits(410),work.Height));
        Size=new Size(Math.Min(Width,work.Width),Math.Min(Height,work.Height));
        if(WindowState==FormWindowState.Normal){
            Location=new Point(Math.Clamp(Left,work.Left,Math.Max(work.Left,work.Right-Width)),Math.Clamp(Top,work.Top,Math.Max(work.Top,work.Bottom-Height)));
        }
        if(mainHost!=null)ResizeViewport(mainHost,mainLayout!,840,650);
        if(homeHost!=null)ResizeViewport(homeHost,home!,760,640);
    }
    static void SetAccessibleStatus(Label label,string value){
        label.Text=value;
        label.AccessibleDescription=value;
    }
    static void PrepareAccessibility(Control root){
        foreach(Control c in root.Controls){
            if(c is Button b)b.AccessibleName??=b.Text.Replace("&","");
            if(c is Label label&&label.AccessibleName!=null){
                var context=label.AccessibleName;
                label.AccessibleName=context+": "+label.Text;label.AccessibleDescription=label.Text;
                label.LiveSetting=System.Windows.Forms.Automation.AutomationLiveSetting.Polite;
                label.TextChanged+=(_,_)=>{
                    label.AccessibleName=context+": "+label.Text;label.AccessibleDescription=label.Text;
                    if(label.IsHandleCreated&&label.Visible)label.AccessibilityObject.RaiseLiveRegionChanged();
                };
            }
            PrepareAccessibility(c);
        }
    }
    void ShowFeedback(){
        using var dialog=new ModernDialog{Text="Feedback e suporte",Size=new Size(660,410),MinimumSize=new Size(460,300),StartPosition=FormStartPosition.CenterParent,BackColor=Bg,ForeColor=Ink,Font=Font,Icon=Icon,AutoScaleMode=AutoScaleMode.Dpi};
        var body=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(16)};
        var note=new Label{Text="Os relatos são públicos no GitHub. Não envie tokens Klei, chaves Steam, senhas, arquivos de mundo ou logs sem revisar. O aplicativo não anexa nem envia dados automaticamente.",AutoSize=true,MaximumSize=new Size(560,0)};
        body.Controls.Add(note);
        var build=typeof(MainForm).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute),false).Cast<System.Reflection.AssemblyMetadataAttribute>().FirstOrDefault(a=>a.Key=="BuildTag")?.Value??"Build local";
        body.Controls.Add(new TextBox{Text=build,ReadOnly=true,Width=LogicalToDeviceUnits(470),BackColor=PanelColor,ForeColor=Ink,AccessibleName="Identificador da build (copiável)"});
        foreach(var option in new (string Text,string Url)[]{
            ("&Relatar bug",FeedbackBase+"/new?template=bug.yml"),
            ("&Sugerir melhoria",FeedbackBase+"/new?template=suggestion.yml"),
            ("Consultar &relatos existentes",FeedbackBase),
            ("&Guia e documentação","https://efac000.github.io/software-dedicado-/#uso")}){
            var b=new ModernButton{Text=option.Text};Style(b);b.Click+=(_,_)=>Open(option.Url);body.Controls.Add(b);
        }
        var close=new ModernButton{Text="Fechar",DialogResult=DialogResult.Cancel};Style(close);body.Controls.Add(close);
        dialog.Controls.Add(body);dialog.CancelButton=close;
        PrepareAccessibility(dialog);dialog.ShowDialog(this);
    }
}