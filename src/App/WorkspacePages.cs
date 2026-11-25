using System.Text.Json;
namespace Gerenciador;
sealed partial class MainForm{
    sealed class BufferedPanel:Panel{public BufferedPanel(){DoubleBuffered=true;ResizeRedraw=true;}}
    sealed class BufferedGrid:DataGridView{public BufferedGrid(){DoubleBuffered=true;}}
    sealed class BufferedTable:TableLayoutPanel{public BufferedTable(){DoubleBuffered=true;ResizeRedraw=true;}}
    Panel? internalPage;
    TaskCompletionSource<bool>? taskPageResult;
    Panel? taskPageHost;
    Button? taskPageCancel;
    Label? taskPageStatus;
    string? lastActivityMessage;
    Action? pendingNavigation;
    bool taskPageAllowsNavigation;
    bool CanNavigateTaskPage=>taskPageAllowsNavigation&&taskPageResult is {Task.IsCompleted:false};
    void NavigateSidebar(string section,Action action){
        if(!busy){action();return;}
        if(!CanNavigateTaskPage||selectedSection==section)return;
        pendingNavigation=action;
        FinishTaskPage(false);
    }
    void EnsureUiContext(){
        if(InvokeRequired)throw new InvalidOperationException("A tarefa visual exige a thread da interface.");
        if(System.Threading.SynchronizationContext.Current is not WindowsFormsSynchronizationContext)
            System.Threading.SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
    }
    async Task<bool> ShowTaskPage(string section,Control content,int width,int height,Button cancel){
        EnsureUiContext();
        if(!busy||taskPageResult!=null)throw new InvalidOperationException("Área de trabalho ocupada.");
        var completion=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);taskPageResult=completion;taskPageCancel=cancel;
        HideInternalPage();homeHost!.Visible=false;mainHost!.Visible=false;
        var shell=new BufferedPanel{BackColor=Bg,ForeColor=Ink};
        var heading=new Label{Dock=DockStyle.Top,Height=LogicalToDeviceUnits(48),Padding=new Padding(16,0,0,0),Font=new Font("Segoe UI Semibold",18),ForeColor=Ink,TextAlign=ContentAlignment.MiddleLeft,Text=section switch{"mods"=>"Catálogo de mods","diagnostic"=>"Diagnóstico","backups"=>"Backups",_=>"Área de trabalho"}};
        content.Dock=DockStyle.Fill;
        var notice=new Label{Dock=DockStyle.Bottom,Height=LogicalToDeviceUnits(32),ForeColor=Muted,BackColor=PanelColor,Padding=new Padding(10,0,10,0),TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true,AccessibleName="Estado da tarefa",Text=status.Text};
        shell.Controls.Add(content);shell.Controls.Add(heading);shell.Controls.Add(notice);
        var viewport=CreateViewport(shell,width,height+80);
        viewport.Name="task-"+section;content.Name="task-content-"+section;
        void StatusChanged(object? sender,EventArgs args)=>SetAccessibleStatus(notice,status.Text);
        try{
            workspace!.SuspendLayout();
            try{workspace.Controls.Add(viewport);viewport.BringToFront();SelectNav(section);ModernControls(content);PrepareAccessibility(shell);if(SystemInformation.HighContrast)UseSystemColors(shell);}
            finally{workspace.ResumeLayout(true);}
            viewport.PerformLayout();shell.PerformLayout();content.PerformLayout();
            taskPageStatus=notice;status.TextChanged+=StatusChanged;
            taskPageHost=viewport;
            // Only idle read-only pages can be abandoned via navigation; writes and catalog editing stay locked.
            taskPageAllowsNavigation=section is "diagnostic" or "backups";
            if(taskPageAllowsNavigation)foreach(var button in navButtons)button.Enabled=true;
            cancel.Focus();
            return await completion.Task;
        }finally{
            taskPageAllowsNavigation=false;foreach(var button in navButtons)button.Enabled=false;
            status.TextChanged-=StatusChanged;taskPageStatus=null;taskPageResult=null;taskPageCancel=null;taskPageHost=null;
            shell.Controls.Remove(content);workspace?.Controls.Remove(viewport);viewport.Dispose();mainHost.Visible=true;mainHost.BringToFront();
        }
    }
    void FinishTaskPage(bool accepted){if(taskPageResult?.TrySetResult(accepted)==true&&taskPageHost!=null){taskPageHost.Enabled=false;foreach(var button in navButtons)button.Enabled=false;}}
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData){
        if(keyData==Keys.Escape&&taskPageCancel is {Enabled:true,Visible:true}){taskPageCancel.PerformClick();return true;}
        return base.ProcessCmdKey(ref msg,keyData);
    }
    static string ActivityPath=>Path.Combine(Path.GetDirectoryName(PreferencesPath)!,"activity-days.json");
    static Dictionary<string,int> ReadActivity(){
        try{
            if(!File.Exists(ActivityPath)||new FileInfo(ActivityPath).Length>16384)return new();
            return (JsonSerializer.Deserialize<Dictionary<string,int>>(File.ReadAllText(ActivityPath))??new()).Where(x=>DateOnly.TryParseExact(x.Key,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out _)&&x.Value>0&&x.Value<=1000000).ToDictionary(x=>x.Key,x=>x.Value);
        }catch{return new();}
    }
    static string DayKey(DateTime day)=>day.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);
    static void RecordActivity(){
        try{
            var day=DayKey(DateTime.Today);var data=ReadActivity();data[day]=Math.Min(1000000,data.GetValueOrDefault(day)+1);
            data=data.OrderByDescending(x=>x.Key,StringComparer.Ordinal).Take(30).ToDictionary(x=>x.Key,x=>x.Value);
            Directory.CreateDirectory(Path.GetDirectoryName(ActivityPath)!);
            var tmp=ActivityPath+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{File.WriteAllText(tmp,JsonSerializer.Serialize(data));File.Move(tmp,ActivityPath,true);}
            finally{if(File.Exists(tmp))File.Delete(tmp);}
        }catch{/* Statistics cannot interrupt maintenance. */}
    }
    void HideInternalPage(){if(internalPage!=null){var old=internalPage;internalPage=null;workspace?.Controls.Remove(old);old.Dispose();}}
    void ShowInternalPage(string section,Control content){
        if(busy){content.Dispose();return;}
        InvalidatePlan();HideInternalPage();homeHost!.Visible=false;mainHost!.Visible=false;
        internalPage=CreateViewport(content,480,520);workspace!.Controls.Add(internalPage);internalPage.BringToFront();
        SelectNav(section);ModernControls(content);PrepareAccessibility(content);
        if(SystemInformation.HighContrast)UseSystemColors(content);
        content.SelectNextControl(null,true,true,true,true);
    }
    TableLayoutPanel NewPage(string title,string subtitle,int rows){
        var page=new BufferedTable{BackColor=Bg,ForeColor=Ink,Padding=new Padding(24,16,24,12),ColumnCount=1,RowCount=rows};
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute,48));page.RowStyles.Add(new RowStyle(SizeType.Absolute,76));
        page.Controls.Add(new Label{Text=title,Font=new Font("Segoe UI Semibold",20),Dock=DockStyle.Fill,ForeColor=Ink,TextAlign=ContentAlignment.MiddleLeft},0,0);
        page.Controls.Add(new Label{Text=subtitle,Dock=DockStyle.Fill,ForeColor=Muted},0,1);
        return page;
    }
    void ApplyLiveTheme(string theme){
        if(busy||SystemInformation.HighContrast)return;
        var before=new[]{Bg,PanelColor,Raised,Ink,Muted,Accent,Border,SelectionColor,WarningColor,SuccessColor};
        ConfigurePalette(theme);
        var after=new[]{Bg,PanelColor,Raised,Ink,Muted,Accent,Border,SelectionColor,WarningColor,SuccessColor};
        Color Map(Color value){for(int i=0;i<before.Length;i++)if(value.ToArgb()==before[i].ToArgb())return after[i];return value;}
        void Cell(DataGridViewCellStyle s){s.BackColor=Map(s.BackColor);s.ForeColor=Map(s.ForeColor);s.SelectionBackColor=Map(s.SelectionBackColor);s.SelectionForeColor=Map(s.SelectionForeColor);}
        void Visit(Control c){
            var oldBack=c.BackColor;var oldFore=c.ForeColor;c.BackColor=Map(oldBack);c.ForeColor=Map(oldFore);
            if(c is Button b){b.FlatAppearance.BorderColor=Map(b.FlatAppearance.BorderColor);b.FlatAppearance.MouseOverBackColor=Map(b.FlatAppearance.MouseOverBackColor);b.FlatAppearance.MouseDownBackColor=Map(b.FlatAppearance.MouseDownBackColor);}
            if(c is DataGridView g){g.BackgroundColor=Map(g.BackgroundColor);g.GridColor=Map(g.GridColor);Cell(g.DefaultCellStyle);Cell(g.ColumnHeadersDefaultCellStyle);Cell(g.AlternatingRowsDefaultCellStyle);Cell(g.RowsDefaultCellStyle);}
            foreach(Control child in c.Controls)Visit(child);c.Invalidate();
        }
        SuspendLayout();try{Visit(this);SelectNav(selectedSection);ApplyCaptionTheme();}finally{ResumeLayout(true);Invalidate(true);}
    }
    void ShowPreferences(){
        if(busy)return;
        var page=NewPage("Preferências","Aparência e caminhos locais. Nenhuma configuração do mundo é alterada.",8);
        foreach(int h in new[]{50,48,48,48,62})page.RowStyles.Add(new RowStyle(SizeType.Absolute,h));
        page.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var theme=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Top,AccessibleName="Tema da interface",BackColor=PanelColor,ForeColor=Ink};
        theme.Items.AddRange(new object[]{"Automático (Windows)","Escuro","Claro"});theme.SelectedIndex=CurrentTheme() switch{"dark"=>1,"light"=>2,_=>0};
        var auto=new CheckBox{Text="Verificar builds ao abrir (consulta ao GitHub)",Checked=checkUpdates,AutoSize=true,AccessibleDescription="Não baixa nem instala automaticamente."};
        var save=new ModernButton{Text="Salvar caminhos selecionados"};Style(save);
        var forget=new ModernButton{Text="Esquecer caminhos salvos"};Style(forget);
        var message=new Label{Dock=DockStyle.Fill,ForeColor=Muted,AccessibleName="Estado das preferências",Text="Escolha o tema e aplique sem reiniciar. O alto contraste do Windows tem prioridade."};
        save.Click+=(_,_)=>{SavePaths();message.Text=homeHint!.Text;};forget.Click+=(_,_)=>{ForgetPaths();message.Text=homeHint!.Text;};
        var apply=new ModernButton{Text="Aplicar preferências"};Style(apply);
        apply.Click+=(_,_)=>{
            bool updateSaved=TrySaveUpdatePreference(UpdatePreference,auto.Checked);
            if(updateSaved)checkUpdates=auto.Checked;
            else auto.Checked=checkUpdates;
            bool themeSaved=false;
            try{
                var selected=new[]{"auto","dark","light"}[theme.SelectedIndex];
                WritePreferenceAtomically(ThemePreference,selected);ApplyLiveTheme(selected);themeSaved=true;
            }catch{theme.SelectedIndex=CurrentTheme() switch{"dark"=>1,"light"=>2,_=>0};}
            message.Text=(updateSaved,themeSaved) switch{
                (true,true)=>"Preferências aplicadas.",
                (false,true)=>"Tema aplicado. Não foi possível salvar a consulta de atualizações; a opção anterior foi mantida.",
                (true,false)=>"Consulta de atualizações salva. Não foi possível aplicar o tema.",
                _=>"Não foi possível salvar as preferências. Confira as permissões e tente novamente."
            };
        };
        var back=new ModernButton{Text="Voltar ao início"};Style(back);back.Click+=(_,_)=>ShowHome();
        var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=true};actions.Controls.AddRange(new Control[]{apply,back});
        page.Controls.Add(theme,0,2);page.Controls.Add(auto,0,3);page.Controls.Add(save,0,4);page.Controls.Add(forget,0,5);page.Controls.Add(message,0,6);page.Controls.Add(actions,0,7);
        ShowInternalPage("preferences",page);
    }
    static string GuidePreference=>Path.Combine(Path.GetDirectoryName(PreferencesPath)!,"guide-seen.txt");
    void OfferFirstRunGuide(){
        if(smokeMode||busy||homeHost?.Visible!=true||File.Exists(GuidePreference)||target.SelectedItem!=null)return;
        ShowGettingStarted();
    }
    void ShowGettingStarted(){
        if(busy)return;
        var page=NewPage("Primeiros passos","Siga a ordem indicada. Nenhuma alteração no mundo sem autorização.",7);
        foreach(int h in new[]{78,78,78,78})page.RowStyles.Add(new RowStyle(SizeType.Absolute,h));
        page.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        void Step(int row,string title,string desc,Action action){
            var b=new ModernButton{Text=title+"\n"+desc,Dock=DockStyle.Fill,AccessibleName=title,AccessibleDescription=desc,BackColor=PanelColor,ForeColor=Ink,FlatStyle=FlatStyle.Flat,TextAlign=ContentAlignment.MiddleLeft};
            DecorateButton(b,true);b.Click+=(_,_)=>action();page.Controls.Add(b,0,row);
        }
        Step(2,"1. Localizar ambiente","Escolha instalação e mundo.",()=>OpenTask(2,false,section:"environment"));
        Step(3,"2. Diagnosticar","Inspeção somente leitura.",()=>OpenTask(2,false,()=>Diagnose(),section:"diagnostic"));
        Step(4,"3. Gerenciar mods","Revise Master e Caves separadamente.",()=>OpenTask(2,false));
        Step(5,"4. Consultar backups","Confira as opções de recuperação.",()=>OpenTask(2,false,()=>OpenBackups(),section:"backups"));
        var back=new ModernButton{Text="Voltar ao início"};Style(back);back.Click+=(_,_)=>ShowHome();page.Controls.Add(back,0,6);
        ShowInternalPage("guide",page);
        if(!smokeMode)try{Directory.CreateDirectory(Path.GetDirectoryName(GuidePreference)!);File.WriteAllText(GuidePreference,"1");}catch{}
    }
    void ShowSessionLog(){
        if(busy)return;
        var page=NewPage("Registro e atividade","Registro detalhado apenas nesta sessão. O gráfico guarda contagens diárias de ciclos de atividade, não resultados.",6);
        page.RowStyles.Add(new RowStyle(SizeType.Absolute,112));page.RowStyles.Add(new RowStyle(SizeType.Absolute,48));page.RowStyles.Add(new RowStyle(SizeType.Percent,100));page.RowStyles.Add(new RowStyle(SizeType.Absolute,56));
        var chart=new BufferedTable{Dock=DockStyle.Fill,ColumnCount=7,RowCount=3,BackColor=PanelColor,Padding=new Padding(6)};
        chart.RowStyles.Add(new RowStyle(SizeType.Absolute,24));chart.RowStyles.Add(new RowStyle(SizeType.Percent,100));chart.RowStyles.Add(new RowStyle(SizeType.Absolute,24));
        for(int i=0;i<7;i++)chart.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/7));
        void RenderChart(){
            foreach(var c in chart.Controls.Cast<Control>().ToArray())c.Dispose();
            var data=ReadActivity();var days=Enumerable.Range(0,7).Select(i=>DateTime.Today.AddDays(i-6)).ToArray();
            var counts=days.Select(d=>data.GetValueOrDefault(DayKey(d))).ToArray();int max=Math.Max(1,counts.Max());
            for(int i=0;i<7;i++){
                int count=counts[i];
                chart.Controls.Add(new Label{Text=count.ToString(),Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Ink},i,0);
                var bar=new BufferedPanel{Dock=DockStyle.Fill,BackColor=PanelColor,AccessibleName=days[i].ToString("dd/MM")+": "+count+" ciclos"};
                bar.Paint+=(_,e)=>{int height=(int)((long)Math.Max(0,bar.Height-4)*count/max);if(height>0){using var brush=new SolidBrush(Accent);e.Graphics.FillRectangle(brush,Math.Max(0,bar.Width/4),bar.Height-height,Math.Max(1,bar.Width/2),height);}};
                chart.Controls.Add(bar,i,1);chart.Controls.Add(new Label{Text=days[i].ToString("dd/MM"),Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Muted},i,2);
            }
        }
        RenderChart();
        var snapshot=log.Text;var contents=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,BackColor=PanelColor,ForeColor=Ink,Text=snapshot.Length==0?"Nenhuma operação nesta sessão.":snapshot,AccessibleName="Registro local das operações"};
        var filter=new TextBox{Width=180,PlaceholderText="Buscar no registro",BackColor=PanelColor,ForeColor=Ink,AccessibleName="Pesquisar registro"};
        filter.TextChanged+=(_,_)=>contents.Text=string.Join(Environment.NewLine,snapshot.Split('\n').Where(line=>line.Contains(filter.Text,StringComparison.OrdinalIgnoreCase)).Select(line=>line.TrimEnd('\r')));
        var copy=new ModernButton{Text="Copiar visualização"};Style(copy);copy.Click+=(_,_)=>{try{if(contents.TextLength>0)Clipboard.SetText(contents.Text);}catch{Notice.Show(this,"Não foi possível copiar. Use Ctrl+C.","Registro");}};
        var tools=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=true,AutoScroll=true};tools.Controls.AddRange(new Control[]{new Label{Text="Pesquisar:",AutoSize=true,ForeColor=Muted,Margin=new Padding(4,8,2,0)},filter,copy});
        var clear=new ModernButton{Text="Apagar contagens locais"};Style(clear);clear.Click+=(_,_)=>{if(Notice.Show(this,"Apagar o histórico local de contagens?","Registro",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;try{if(File.Exists(ActivityPath))File.Delete(ActivityPath);RenderChart();}catch{Notice.Show(this,"Não foi possível apagar o histórico.","Registro");}};
        var back=new ModernButton{Text="Voltar ao início"};Style(back);back.Click+=(_,_)=>ShowHome();
        var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=true};footer.Controls.AddRange(new Control[]{clear,back});
        page.Controls.Add(chart,0,2);page.Controls.Add(tools,0,3);page.Controls.Add(contents,0,4);page.Controls.Add(footer,0,5);
        ShowInternalPage("log",page);
    }
}
