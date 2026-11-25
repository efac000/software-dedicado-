using System.Text.Json;
using Dedicado;
namespace Gerenciador;
sealed partial class MainForm{
    TableLayoutPanel? home;
    Label? homeState,homeHint,homeActivity;
    int sessionEvents;DateTime? lastEvent;
    readonly List<Button> homeActions=new();
    Button? homeDetect;
    bool homeReady;
    bool checkUpdates;
    static Color SelectionColor=Color.FromArgb(40,68,109),WarningColor=Color.FromArgb(255,190,112),SuccessColor=Color.FromArgb(122,221,176);
    static string ThemePreference=>Path.Combine(Path.GetDirectoryName(PreferencesPath)!,"theme.txt");
    static string CurrentTheme(){
        try{if(File.Exists(ThemePreference)&&new FileInfo(ThemePreference).Length<=16){var s=File.ReadAllText(ThemePreference).Trim();if(s is "dark" or "light" or "auto")return s;}}catch{}
        return "auto";
    }
    internal static void ConfigurePalette(string? forced=null){
        var theme=forced??CurrentTheme();
        var light=theme=="light";
        if(theme=="auto")try{light=Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","AppsUseLightTheme",0) is int value&&value==1;}catch{}
        if(SystemInformation.HighContrast||theme=="contrast"){
            Bg=PanelColor=Raised=SystemColors.Window;Ink=Muted=WarningColor=SuccessColor=SystemColors.WindowText;Accent=SystemColors.Highlight;Border=SystemColors.WindowText;SelectionColor=SystemColors.Highlight;
        }else if(light){
            Bg=Color.FromArgb(246,248,252);PanelColor=Color.White;Raised=Color.FromArgb(224,235,247);Ink=Color.FromArgb(24,38,60);Muted=Color.FromArgb(70,86,108);Accent=Color.FromArgb(16,88,130);Border=Color.FromArgb(130,149,173);SelectionColor=Color.FromArgb(205,225,247);WarningColor=Color.FromArgb(145,71,15);SuccessColor=Color.FromArgb(18,104,64);
        }else{
            Bg=Color.FromArgb(12,17,27);PanelColor=Color.FromArgb(20,29,43);Ink=Color.FromArgb(237,244,255);Accent=Color.FromArgb(119,213,249);Border=Color.FromArgb(51,70,101);Muted=Color.FromArgb(170,187,211);Raised=Color.FromArgb(23,38,64);SelectionColor=Color.FromArgb(40,68,109);WarningColor=Color.FromArgb(255,190,112);SuccessColor=Color.FromArgb(122,221,176);
        }
    }
    static string UpdatePreference=>Path.Combine(Path.GetDirectoryName(PreferencesPath)!,"check-updates.txt");
    static bool TrySaveUpdatePreference(string path,bool enabled){
        try{
            if(enabled)WritePreferenceAtomically(path,"true");
            else File.Delete(path);
            return true;
        }catch{return false;}
    }
    static void WritePreferenceAtomically(string path,string value){
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{File.WriteAllText(temporary,value);File.Move(temporary,path,true);}
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    async Task CheckOnStartup(){
        if(checkUpdates)await CheckBuild(true);
    }
    string? preferredWorld;
    sealed record LocalPaths(string Install,string Worlds,string? World);
    static string PreferencesPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Gerenciador de Dedicado","paths.json");
    void BuildHome(){
        home=new BufferedTable{Dock=DockStyle.Fill,Padding=new Padding(24,16,24,12),ColumnCount=1,RowCount=8,BackColor=Bg};
        foreach(var h in new[]{48,58,80,46})home.RowStyles.Add(new RowStyle(SizeType.Absolute,h));
        home.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        home.RowStyles.Add(new RowStyle(SizeType.Absolute,92));
        home.RowStyles.Add(new RowStyle(SizeType.Absolute,58));
        home.RowStyles.Add(new RowStyle(SizeType.Absolute,40));
        var brand=new Label{Text="Visão geral",Font=new Font("Segoe UI Semibold",20),Dock=DockStyle.Fill,ForeColor=Ink,AutoEllipsis=true,TextAlign=ContentAlignment.MiddleLeft};
        home.Controls.Add(brand,0,0);
        var intro=new Label{Text="Gerenciador de Dedicado · 1.0.0\nEscolha uma tarefa. Nenhuma alteração sem sua autorização.",Dock=DockStyle.Fill,ForeColor=Ink,TextAlign=ContentAlignment.MiddleLeft};
        home.Controls.Add(intro,0,1);
        homeState=new Label{Dock=DockStyle.Fill,BackColor=Raised,ForeColor=Ink,Padding=new Padding(12,5,12,5),TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true,AccessibleName="Estado da seleção do servidor"};
        home.Controls.Add(homeState,0,2);
        var setup=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,AutoScroll=true};
        homeDetect=new ModernButton{Text="&Detectar e escolher"};Style(homeDetect);
        homeDetect.Click+=async(_,_)=>{await AutoLocate();RefreshHome();};
        var manual=new ModernButton{Text="&Escolher pasta"};Style(manual);manual.Click+=(_,_)=>OpenTask(2,false,section:"environment");
        var prefs=new ModernButton{Text="&Preferências"};Style(prefs);prefs.Click+=(_,_)=>ShowPreferences();
        setup.Controls.AddRange(new Control[]{homeDetect,manual,prefs});
        homeActions.AddRange(new[]{homeDetect,manual,prefs});
        home.Controls.Add(setup,0,3);
        var cards=new BufferedTable{Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,BackColor=Bg,CellBorderStyle=TableLayoutPanelCellBorderStyle.None};
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        cards.RowStyles.Add(new RowStyle(SizeType.Percent,50));cards.RowStyles.Add(new RowStyle(SizeType.Percent,50));
        void Card(string title,string description,Action run,int x,int y){
            var b=new ModernButton{Text=title+"\n"+description,AccessibleName=title.Replace("&",""),AccessibleDescription=description,Dock=DockStyle.Fill,Margin=new Padding(4),TextAlign=ContentAlignment.MiddleLeft};
            b.AutoSize=false;b.FlatStyle=FlatStyle.Flat;b.BackColor=PanelColor;b.ForeColor=Ink;b.FlatAppearance.BorderColor=Border;DecorateButton(b,true);b.Font=new Font("Segoe UI",12);b.Padding=new Padding(12,4,12,4);b.Click+=(_,_)=>run();
            cards.Controls.Add(b,x,y);homeActions.Add(b);
        }
        Card("&Gerenciar mods","Adicionar, remover e configurar",()=>OpenTask(2,false),0,0);
        Card("&Atualizar servidor","SteamCMD e validação",()=>OpenTask(2,false,()=>UpdateDedicated(),true,"update"),1,0);
        Card("Diagn&osticar","Identificar problemas sem alterar arquivos",()=>OpenTask(2,false,()=>Diagnose(),section:"diagnostic"),0,1);
        Card("&Backups","Consultar e recuperar configurações",()=>OpenTask(2,false,()=>OpenBackups(),section:"backups"),1,1);
        home.Controls.Add(cards,0,4);
        var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=false,WrapContents=true};
        var advanced=new ModernButton{Text="&Ferramentas avançadas"};Style(advanced);advanced.Click+=(_,_)=>OpenTask(2,true);
        var help=new ModernButton{Text="&Como usar"};Style(help);help.Click+=(_,_)=>ShowGettingStarted();
        var updates=new ModernButton{Text="Verificar at&ualização"};Style(updates);updates.Click+=async(_,_)=>await CheckBuild();
        footer.Controls.AddRange(new Control[]{advanced,help,updates});homeActions.AddRange(new[]{advanced,help,updates});
        var history=new ModernButton{Text="&Registro"};Style(history);history.Click+=(_,_)=>ShowSessionLog();
        footer.Controls.Add(history);homeActions.Add(history);
        var feedback=new ModernButton{Text="Feedbac&k e suporte"};Style(feedback);feedback.AccessibleDescription="Abre opções para relatar bugs ou sugerir melhorias no GitHub. Nenhum dado é enviado automaticamente.";
        feedback.Click+=(_,_)=>ShowFeedback();footer.Controls.Add(feedback);homeActions.Add(feedback);
        home.Controls.Add(footer,0,5);
        homeHint=new Label{Dock=DockStyle.Fill,ForeColor=Muted,AutoEllipsis=true,TextAlign=ContentAlignment.MiddleLeft,AccessibleName="Orientação da tela inicial"};
        homeActivity=new Label{Dock=DockStyle.Fill,BackColor=PanelColor,ForeColor=Muted,Padding=new Padding(10,4,10,4),TextAlign=ContentAlignment.MiddleLeft,AccessibleName="Atividade recente",AutoEllipsis=true};
        home.Controls.Add(homeActivity,0,6);home.Controls.Add(homeHint,0,7);
        homeHost=CreateViewport(home,760,640);Controls.Add(homeHost);homeHost.Visible=false;homeReady=true;
        if(SystemInformation.HighContrast)UseSystemColors(home);
        RefreshHome();
    }
    static void UseSystemColors(Control root){
        root.BackColor=SystemColors.Window;root.ForeColor=SystemColors.WindowText;
        if(root is Button b){b.FlatStyle=FlatStyle.System;b.UseVisualStyleBackColor=true;}
        foreach(Control c in root.Controls)UseSystemColors(c);
    }
    void ShowHome(){
        if(busy||home==null)return;
        InvalidatePlan();mainHost!.Visible=false;HideInternalPage();homeHost!.Visible=true;homeHost.BringToFront();homeHost.AutoScrollPosition=Point.Empty;
        SelectNav("home");RefreshHome();homeDetect?.Focus();
    }
    void OpenTask(int selectedMode,bool advanced,Func<Task>? action=null,bool needsSelection=false,string? section=null){
        if(busy||home==null)return;
        InvalidatePlan();HideInternalPage();homeHost!.Visible=false;mainHost!.Visible=true;mainHost.BringToFront();mainHost.AutoScrollPosition=Point.Empty;
        if(mode.SelectedIndex!=selectedMode)mode.SelectedIndex=selectedMode;
        mainLayout!.RowStyles[1].Height=advanced?LogicalToDeviceUnits(46):0;mode.Parent!.Visible=advanced;
        RefreshMode();SelectNav(section??(advanced?"advanced":"mods"));
        if(action!=null){
            if(needsSelection&&(target.SelectedItem==null||string.IsNullOrWhiteSpace(install.Text))){
                status.Text="Escolha a instalação e o mundo. Depois use o botão da tarefa desejada.";
                target.Focus();return;
            }
            _ = RunHomeAction(action);return;
        }
        if(target.SelectedItem!=null)analyze.Focus();else target.Focus();
    }
    async Task RunHomeAction(Func<Task> action){
        try{await action();}catch(Exception e){Write(e.Message);status.Text="Não foi possível concluir. Consulte o registro e tente novamente.";}
        RefreshHome();
    }
    void RefreshHome(){
        if(!homeReady||homeState==null||homeHint==null)return;
        foreach(var b in homeActions)b.Enabled=!busy;RefreshActivity();
        if(busy){SetAccessibleStatus(homeState,"Localizando ou verificando dados…");return;}
        var selected=target.SelectedItem as Cluster;
        bool installed=!string.IsNullOrWhiteSpace(install.Text)&&Directory.Exists(install.Text);
        var state=installed?(selected!=null?"Servidor e mundo selecionados (validação na análise)":"Instalação selecionada; escolha o mundo."):
            "Instalação dedicada não selecionada. Use Detectar e escolher.";
        var world=selected==null?"Mundo: não selecionado":"Mundo: "+selected.Name+" | "+selected.FolderName;
        var steam=selectedSteamCmd!=null&&File.Exists(selectedSteamCmd)?"SteamCMD: localizado":"SteamCMD: verificar na atualização";
        SetAccessibleStatus(homeState,state+"\n"+world+"    •    "+steam);
        SetAccessibleStatus(homeHint,recoveryBlocked?recoveryNotice.Text:"Copiar mundos, reinstalar e editar Lua ficam em Ferramentas avançadas.");
    }
    void RefreshActivity(){
        if(homeActivity==null)return;
        var value=busy?"Atividade recente · Operação em andamento":
            lastEvent.HasValue?$"Atividade recente · {sessionEvents} eventos nesta sessão | último às {lastEvent:HH:mm:ss}":
            "Atividade recente · Nenhuma operação nesta sessão.";
        if(!busy&&!string.IsNullOrWhiteSpace(lastActivityMessage))value+="\n"+lastActivityMessage;
        SetAccessibleStatus(homeActivity,value);
    }
    void LoadPaths(){
        checkUpdates=File.Exists(UpdatePreference);
        try{
            if(!File.Exists(PreferencesPath)||new FileInfo(PreferencesPath).Length>16384)return;
            var paths=JsonSerializer.Deserialize<LocalPaths>(File.ReadAllText(PreferencesPath));
            if(paths==null)return;
            if(Path.IsPathFullyQualified(paths.Install)&&Directory.Exists(paths.Install))install.Text=paths.Install;
            if(Path.IsPathFullyQualified(paths.Worlds)&&Directory.Exists(paths.Worlds)){destinationRoot.Text=paths.Worlds;preferredWorld=paths.World;}
        }catch{Write("Não foi possível ler os caminhos salvos. Use a detecção manual.");}
    }
    void SelectSavedWorld(){
        if(preferredWorld==null)return;
        var matches=target.Items.Cast<Cluster>().Where(c=>string.Equals(c.Path,preferredWorld,StringComparison.OrdinalIgnoreCase)).ToArray();
        if(matches.Length==1)target.SelectedItem=matches[0];
        preferredWorld=null;RefreshHome();
    }
    void SavePaths(){
        if(busy)return;
        if(string.IsNullOrWhiteSpace(install.Text)||!Directory.Exists(install.Text)||target.SelectedItem is not Cluster world){
            homeHint!.Text="Escolha a instalação e o mundo antes de salvar.";return;
        }
        try{
            Directory.CreateDirectory(Path.GetDirectoryName(PreferencesPath)!);
            var tmp=PreferencesPath+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{File.WriteAllText(tmp,JsonSerializer.Serialize(new LocalPaths(install.Text,destinationRoot.Text,world.Path)));File.Move(tmp,PreferencesPath,true);}
            finally{if(File.Exists(tmp))File.Delete(tmp);}
            homeHint!.Text="Caminhos salvos neste Windows. A confirmação de segurança nunca é salva.";
        }catch{homeHint!.Text="Não foi possível salvar os caminhos. Você pode continuar normalmente.";}
    }
    void ForgetPaths(){
        try{if(File.Exists(PreferencesPath))File.Delete(PreferencesPath);homeHint!.Text="Caminhos esquecidos. Nenhum arquivo do mundo foi alterado.";}
        catch{homeHint!.Text="Não foi possível remover os caminhos salvos.";}
    }
    async Task CheckBuild(bool quiet=false){
        if(busy)return;
        Busy(true);
        string message;
        try{
            using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(12));using var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(12)};
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Gerenciador-de-Dedicado/1.0.0");
            using var response=await http.GetAsync("https://api.github.com/repos/efac000/software-dedicado-/releases/latest",HttpCompletionOption.ResponseHeadersRead,deadline.Token);
            response.EnsureSuccessStatusCode();
            using var stream=await response.Content.ReadAsStreamAsync(deadline.Token);
            using var data=new MemoryStream();var bytes=new byte[4096];
            int n;while((n=await stream.ReadAsync(bytes,deadline.Token))>0){if(data.Length+n>262144)throw new IOException();data.Write(bytes,0,n);}
            using var json=JsonDocument.Parse(data.ToArray());
            var tag=json.RootElement.GetProperty("tag_name").GetString()??"";
            if(!System.Text.RegularExpressions.Regex.IsMatch(tag,@"^1\.0\.0-build-\d+-\d+$"))throw new IOException();
            var currentBuild=typeof(MainForm).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute),false).Cast<System.Reflection.AssemblyMetadataAttribute>().FirstOrDefault(a=>a.Key=="BuildTag")?.Value;
            message=tag==currentBuild?"Esta é a build estável atual.":currentBuild==null||currentBuild==""?"Build estável disponível: "+tag+". Compare com a versão baixada.":"Build estável disponível: "+tag+". Verifique no site oficial.";
        }catch{message="Não foi possível verificar a atualização agora. O programa continua utilizável offline.";}
        finally{Busy(false);}
        SetAccessibleStatus(homeHint!,message);
        if(!quiet&&Notice.Show(this,message+"\n\nAbrir o site oficial?","Atualizações",MessageBoxButtons.YesNo,MessageBoxIcon.Information,MessageBoxDefaultButton.Button2)==DialogResult.Yes)Open("https://efac000.github.io/software-dedicado-/#baixar");
    }
    internal bool VerifyHomeLayout(){
        if(home==null)return false;
        busy=false;ShowHome();
        foreach(var size in new[]{MinimumSize,new Size(Math.Max(MinimumSize.Width,1100),Math.Max(MinimumSize.Height,830))}){
            Size=size;PerformLayout();ResizeViewport(homeHost!,home,760,640);home.PerformLayout();
            foreach(var b in homeActions){
                if(b.Width<=0||b.Height<=0||string.IsNullOrWhiteSpace(b.AccessibleName??b.Text))return false;
                for(Control? c=b;c?.Parent!=null&&c.Parent!=homeHost;c=c.Parent)if(!c.Parent.ClientRectangle.Contains(c.Bounds))return false;
            }
        }
        if(homeActions.Count!=12)return false;
        OpenTask(2,false);
        if(mainLayout!.RowStyles[1].Height!=0)return false;
        exclusive.Checked=true;ShowHome();
        if(exclusive.Checked||reset.Enabled)return false;
        OpenTask(2,true);
        if(mainLayout.RowStyles[1].Height!=LogicalToDeviceUnits(46))return false;
        ShowHome();
        Busy(true);if(homeActions.Any(b=>b.Enabled))return false;
        return true;
    }
}