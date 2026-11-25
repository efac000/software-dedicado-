using System.Diagnostics;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Dedicado;
namespace Gerenciador;
static class Program {
    [STAThread] static int Main(string[] args){
        if(args.Length==1&&args[0]=="--self-test")return Engine.ValidNumber("1")&&!Engine.ValidNumber("1a")&&Lua.Active("return {}").Count==0?0:1;
        if(args.Length==1&&args[0]=="--self-test-ui"){
            ApplicationConfiguration.Initialize();
            using var form=new MainForm();
            using var associated=System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if(associated==null||form.Icon==null||form.Handle==IntPtr.Zero)return 1;
            using var actual=form.Icon.ToBitmap();
            using var expected=associated.ToBitmap();
            if(actual.Size!=expected.Size)return 1;
            for(int y=0;y<actual.Height;y++)for(int x=0;x<actual.Width;x++)
                if(actual.GetPixel(x,y)!=expected.GetPixel(x,y))return 1;
            return 0;
        }
        ApplicationConfiguration.Initialize();Application.Run(new MainForm());return 0;
    }
}
sealed partial class MainForm:Form{
    static readonly Color Bg=Color.FromArgb(9,15,30), PanelColor=Color.FromArgb(16,27,48), Ink=Color.FromArgb(237,244,255), Accent=Color.FromArgb(119,213,249), Border=Color.FromArgb(51,70,101), Muted=Color.FromArgb(170,187,211), Raised=Color.FromArgb(23,38,64);
    readonly TextBox root=new(),destinationRoot=new(),install=new(),log=new(){Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical};
    readonly ComboBox source=new(){DropDownStyle=ComboBoxStyle.DropDownList},target=new(){DropDownStyle=ComboBoxStyle.DropDownList};
    readonly DataGridView grid=new(){ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,AllowUserToResizeRows=false};
    readonly CheckBox exclusive=new(){Text="Confirmo: instalação exclusiva, UGC padrão, servidor e reinício automático desligados.",AutoSize=true};
    readonly CheckBox online=new(){Text="Consultar nomes na Steam Workshop (envia somente IDs públicos).",Checked=true,AutoSize=true};
    readonly Button diagnose=new(){Text="Diagnóstico do dedicado"};
    readonly Button scan=new(){Text="Localizar clusters"},analyze=new(){Text="Analisar (sem alterações)"},reset=new(){Text="Confirmar e reinstalar",Enabled=false},backups=new(){Text="Central de backups"},website=new(){Text="Site / atualizações"};
    readonly Label status=new(){Text="Selecione as pastas e os mundos.",AutoSize=false,Dock=DockStyle.Fill,ForeColor=Muted,AutoEllipsis=true};
    readonly ComboBox mode=new(){DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
    Control? sourceRow, sourceClusterRow;
    TableLayoutPanel? mainLayout;
    readonly Label modeHint=new(){Dock=DockStyle.Fill,ForeColor=Muted,TextAlign=ContentAlignment.MiddleLeft};
    Plan? current; bool busy; readonly Dictionary<string,string[]> publishedDependencies=new(StringComparer.Ordinal);
    public MainForm(){
        Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Text="Gerenciador de Dedicado | 1.0.0";MinimumSize=new Size(850,680);Size=new Size(1100,830);AutoScaleMode=AutoScaleMode.Dpi;StartPosition=FormStartPosition.CenterScreen;BackColor=Bg;ForeColor=Ink;Font=new Font("Segoe UI",11);
        var main=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(24,16,24,12),ColumnCount=1,RowCount=14,BackColor=Bg};
        mainLayout=main;
        foreach(var height in new[]{66,35,29,40,40,40,40,40,40})
            main.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
        main.RowStyles.Add(new RowStyle(SizeType.Percent,72));
        main.RowStyles.Add(new RowStyle(SizeType.Percent,28));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute,34));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute,40));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute,30));
        Controls.Add(main);

        var top=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var heading=new Label{Text="Gerenciador de Dedicado",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Font=new Font("Segoe UI Semibold",23),ForeColor=Ink};
        var updater=new Button{Text="Atualizar servidor",Anchor=AnchorStyles.Right};
        Style(updater);
        updater.Click+=async(_,_)=>await UpdateDedicated();
        top.Controls.Add(heading);
        top.Controls.Add(updater);
        main.Controls.Add(top);

        mode.Items.AddRange(new object[]{"Copiar mods de outro mundo","Reinstalar mods deste servidor","Gerenciar mods visualmente","Editar Lua (avançado)"});
        mode.AccessibleName="Modo de manutenção";
        mode.SelectedIndex=2;
        foreach(var combo in new[]{mode,source,target}){
            combo.BackColor=PanelColor;
            combo.ForeColor=Ink;
            combo.FlatStyle=FlatStyle.Flat;
            combo.Anchor=AnchorStyles.Left|AnchorStyles.Right;
        }
        main.Controls.Add(SelectionRow("Modo de manutenção",mode));
        main.Controls.Add(modeHint);
        sourceRow=PathRow("Pasta dos mundos de origem",root);
        main.Controls.Add(sourceRow);
        sourceClusterRow=SelectionRow("Mundo de origem",source);
        main.Controls.Add(sourceClusterRow);
        main.Controls.Add(PathRow("Pasta dos mundos de destino",destinationRoot));
        main.Controls.Add(SelectionRow("Mundo de destino",target,scan));
        install.PlaceholderText="Selecione a pasta do servidor dedicado";
        main.Controls.Add(PathRow("Servidor dedicado",install));

        online.Text="Consultar nomes dos mods na Workshop";
        online.Anchor=AnchorStyles.Left;
        analyze.Text="Analisar seleção";
        var choices=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};
        choices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        choices.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        choices.Controls.Add(online);
        choices.Controls.Add(analyze);
        main.Controls.Add(choices);
        grid.Dock=DockStyle.Fill;grid.BackgroundColor=PanelColor;grid.BorderStyle=BorderStyle.None;grid.EnableHeadersVisualStyles=false;grid.ColumnHeadersDefaultCellStyle.BackColor=PanelColor;grid.ColumnHeadersDefaultCellStyle.ForeColor=Ink;grid.DefaultCellStyle.BackColor=Bg;grid.DefaultCellStyle.ForeColor=Ink;grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(40,68,109);grid.GridColor=Border;grid.AlternatingRowsDefaultCellStyle.BackColor=Raised;grid.ColumnHeadersDefaultCellStyle.SelectionBackColor=PanelColor;grid.ColumnHeadersDefaultCellStyle.Font=new Font("Segoe UI",10,FontStyle.Bold);grid.ColumnHeadersHeight=34;grid.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;grid.DefaultCellStyle.WrapMode=DataGridViewTriState.False;grid.DefaultCellStyle.SelectionForeColor=Ink;grid.RowTemplate.Height=30;
        foreach(var h in new[]{"Mod / Workshop","ID","Shard","Alteração"})grid.Columns.Add(h,h);
        grid.Columns[0].FillWeight=180;main.Controls.Add(grid);
        log.Dock=DockStyle.Fill;log.BackColor=PanelColor;log.ForeColor=Ink;log.BorderStyle=BorderStyle.FixedSingle;log.Font=new Font("Consolas",10);main.Controls.Add(log);
        main.Controls.Add(exclusive);
        reset.Text="Revisar e aplicar";
        var actions=new FlowLayoutPanel{Dock=DockStyle.Fill};actions.Controls.Add(reset);actions.Controls.Add(diagnose);actions.Controls.Add(backups);actions.Controls.Add(website);main.Controls.Add(actions);main.Controls.Add(status);
        for(var row=0;row<main.Controls.Count;row++)main.SetCellPosition(main.Controls[row],new TableLayoutPanelCellPosition(0,row));
        foreach(var b in new[]{scan,analyze,reset,diagnose,backups,website})Style(b);analyze.BackColor=Accent;analyze.ForeColor=Bg;analyze.FlatAppearance.MouseOverBackColor=Color.FromArgb(163,230,255);analyze.FlatAppearance.MouseDownBackColor=Color.FromArgb(100,190,230);
        root.Text=DefaultClusterRoot();
        destinationRoot.Text=root.Text;
        install.Text=FindInstall();
        root.TextChanged+=(_,_)=>{InvalidatePlan();source.Items.Clear();};destinationRoot.TextChanged+=(_,_)=>{InvalidatePlan();target.Items.Clear();};install.TextChanged+=(_,_)=>InvalidatePlan();source.SelectedIndexChanged+=(_,_)=>InvalidatePlan();target.SelectedIndexChanged+=(_,_)=>InvalidatePlan();exclusive.CheckedChanged+=(_,_)=>reset.Enabled=!busy&&current!=null&&exclusive.Checked;online.CheckedChanged+=(_,_)=>InvalidatePlan();
        scan.Click+=(_,_)=>{if(string.IsNullOrWhiteSpace(install.Text))install.Text=FindInstall();RefreshClusters();};analyze.Click+=async(_,_)=>await Analyze();reset.Click+=async(_,_)=>await Reset();diagnose.Click+=async(_,_)=>await Diagnose();backups.Click+=async(_,_)=>await OpenBackups();website.Click+=(_,_)=>Open("https://efac000.github.io/software-dedicado-/");
        FormClosing+=(_,e)=>{if(busy){e.Cancel=true;MessageBox.Show("A operação está em andamento. Aguarde a conclusão.");}};
        mode.SelectedIndexChanged+=(_,_)=>{InvalidatePlan();RefreshMode();RefreshClusters();};
        RefreshMode();Shown+=(_,_)=>RefreshClusters();
    }
    static void Style(Button b){b.AutoSize=true;b.MinimumSize=new Size(0,32);b.FlatStyle=FlatStyle.Flat;b.BackColor=PanelColor;b.ForeColor=Ink;b.FlatAppearance.BorderColor=Border;b.FlatAppearance.MouseOverBackColor=Raised;b.FlatAppearance.MouseDownBackColor=Color.FromArgb(40,68,109);b.Padding=new Padding(8,0,8,0);}
    Control SelectionRow(string title,Control field,Button? button=null){
        var row=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=button==null?2:3};
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,226));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        row.Controls.Add(new Label{Text=title,AutoSize=true,Anchor=AnchorStyles.Left,ForeColor=Ink});
        field.AccessibleName=title;
        field.Anchor=AnchorStyles.Left|AnchorStyles.Right;
        row.Controls.Add(field);
        if(button!=null){
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.Controls.Add(button);
        }
        return row;
    }
    Control PathRow(string title,TextBox box){
        box.BackColor=PanelColor;
        box.ForeColor=Ink;
        box.BorderStyle=BorderStyle.FixedSingle;
        var browse=new Button{Text="Procurar…"};
        Style(browse);
        browse.Click+=(_,_)=>{
            using var dialog=new FolderBrowserDialog{Description=title};
            if(Directory.Exists(box.Text))dialog.SelectedPath=box.Text;
            if(dialog.ShowDialog(this)!=DialogResult.OK)return;
            box.Text=dialog.SelectedPath;
            if(box==root||box==destinationRoot)RefreshClusters();
        };
        return SelectionRow(title,box,browse);
    }
    static string DefaultClusterRoot(){
        var dirs=new[]{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Documents"),Path.Combine(Environment.GetEnvironmentVariable("OneDrive")??"","Documents"),Path.Combine(Environment.GetEnvironmentVariable("OneDriveConsumer")??"","Documents")};
        var candidates=dirs.Where(Path.IsPathFullyQualified).Select(d=>Path.Combine(d,"Klei","DoNotStarveTogether")).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach(var c in candidates)try{if(Engine.Discover(c).Length>0)return c;}catch{}
        return candidates.FirstOrDefault(Directory.Exists)??candidates.FirstOrDefault()??"";
    }
    static string FindInstall(){
        var candidates=new List<string>();
        foreach(var key in new[]{@"HKEY_CURRENT_USER\Software\Valve\Steam",@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam"}){
            try{if(Registry.GetValue(key,"SteamPath",null) is string s)candidates.Add(s);if(Registry.GetValue(key,"InstallPath",null) is string p)candidates.Add(p);}catch{}
        }
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Steam"));
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Steam"));
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"steamcmd"));
        candidates.Add(@"C:\steamcmd");
        foreach(var steam in candidates.ToArray())try{var vdf=Path.Combine(steam,"steamapps","libraryfolders.vdf");if(File.Exists(vdf))foreach(Match m in Regex.Matches(File.ReadAllText(vdf),""" "path"\s+"([^"]+)" """.Trim()))candidates.Add(m.Groups[1].Value.Replace(@"\\",@"\"));}catch{}
        foreach(var library in candidates.Distinct(StringComparer.OrdinalIgnoreCase)){
            foreach(var p in new[]{Path.Combine(library,"steamapps","common","Don't Starve Together Dedicated Server"),Path.Combine(library,"dst"),Path.Combine(library,"server"),Path.Combine(library,"Don't Starve Together Dedicated Server")})try{Engine.FindExe(p);return p;}catch{}
        }
        return "";
    }
    void InvalidatePlan(){publishedDependencies.Clear();current=null;reset.Enabled=false;grid.Rows.Clear();status.Text="Analise novamente antes de executar.";}
    bool InPlace=>mode.SelectedIndex!=0;
    void RefreshMode(){
        source.Enabled=!busy&&!InPlace;
        sourceRow!.Visible=sourceClusterRow!.Visible=!InPlace;
        mainLayout!.RowStyles[3].Height=mainLayout.RowStyles[4].Height=InPlace?0:40;
        mode.Enabled=!busy;
        modeHint.Text=mode.SelectedIndex switch{
            0=>"Selecione o mundo de origem e o servidor que receberá os mods.",
            1=>"Reinstale os mods mantendo as opções atuais.",
            3=>"Edite as configurações Lua por shard antes de revisar o plano.",
            _=>"Selecione o servidor e clique em Analisar seleção para abrir o catálogo de mods."
        };
    }
    void RefreshClusters(){
        InvalidatePlan();source.Items.Clear();target.Items.Clear();
        try{if(!InPlace)foreach(var c in Engine.Discover(root.Text))source.Items.Add(c);foreach(var c in Engine.Discover(destinationRoot.Text))target.Items.Add(c);if(target.Items.Count==1)target.SelectedIndex=0;if(source.Items.Count==1)source.SelectedIndex=0;status.Text=string.IsNullOrWhiteSpace(install.Text)
                ?"Selecione a pasta do servidor dedicado para continuar."
                :InPlace?$"Mundos de destino encontrados: {target.Items.Count}.":$"Mundos encontrados: {source.Items.Count} na origem e {target.Items.Count} no destino.";if((!InPlace&&source.Items.Count==0)||target.Items.Count==0)status.Text+=" Confira as pastas ou selecione-as manualmente.";}
        catch(Exception e){Write(e.Message);}
    }
    void Write(string s){if(InvokeRequired){BeginInvoke(new Action(()=>Write(s)));return;}log.AppendText(DateTime.Now.ToString("HH:mm:ss")+"  "+s+Environment.NewLine);}
    void Busy(bool b){
        busy=b;root.Enabled=destinationRoot.Enabled=install.Enabled=source.Enabled=target.Enabled=scan.Enabled=analyze.Enabled=exclusive.Enabled=online.Enabled=!b;
        // Disable path-picker buttons as well.
        foreach(Control row in Controls[0].Controls)if(row is TableLayoutPanel)row.Enabled=!b;
        diagnose.Enabled=backups.Enabled=!b;RefreshMode();reset.Enabled=!b&&current!=null&&exclusive.Checked;
    }
    async Task Diagnose(){
        var cluster=target.SelectedItem is Cluster c?c.Path:destinationRoot.Text;
        if(target.SelectedItem is not Cluster&&!File.Exists(Path.Combine(cluster,"cluster.ini"))){
            using var picker=new FolderBrowserDialog{Description="Selecione a pasta do cluster a diagnosticar (mesmo incompleto)"};
            if(Directory.Exists(cluster))picker.SelectedPath=cluster;
            if(picker.ShowDialog(this)!=DialogResult.OK)return;cluster=picker.SelectedPath;
        }
        var installation=install.Text;var next=DiagnosticAction.None;Busy(true);
        try{
            status.Text="Diagnóstico em leitura...";
            var findings=await Task.Run(()=>Diagnostics.Inspect(cluster,installation,Write));
            using var dialog=new Form{Text="Diagnóstico do dedicado — 1.0.0",Size=new Size(1020,640),MinimumSize=new Size(650,420),StartPosition=FormStartPosition.CenterParent,BackColor=Bg,ForeColor=Ink,Font=Font};
            var header=new Label{Dock=DockStyle.Top,Height=65,ForeColor=Accent,Text=$"{findings.Count(x=>x.Level==Severity.Error)} erros de inspeção/configuração • {findings.Count(x=>x.Level==Severity.Warning)} avisos • {findings.Count(x=>x.Level==Severity.Info)} informações\nSomente leitura. Indícios em logs podem ser históricos."};
            var list=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells,BackgroundColor=PanelColor,EnableHeadersVisualStyles=false};
            list.ColumnHeadersDefaultCellStyle.BackColor=Raised;list.ColumnHeadersDefaultCellStyle.ForeColor=Ink;list.DefaultCellStyle.BackColor=Bg;list.DefaultCellStyle.ForeColor=Ink;list.DefaultCellStyle.SelectionBackColor=Raised;list.DefaultCellStyle.SelectionForeColor=Accent;list.DefaultCellStyle.WrapMode=DataGridViewTriState.True;
            foreach(var name in new[]{"Gravidade","Local","Problema","Ação recomendada"})list.Columns.Add(name,name);
            list.Columns[0].FillWeight=45;list.Columns[1].FillWeight=75;list.Columns[2].FillWeight=150;list.Columns[3].FillWeight=180;

            var filter=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=160,BackColor=PanelColor,ForeColor=Ink};
            filter.Items.AddRange(new object[]{"Todos","Erros","Avisos","Informações"});filter.SelectedIndex=0;
            var query=new TextBox{Width=225,PlaceholderText="Filtrar problema, ID ou local",BackColor=PanelColor,ForeColor=Ink};
            var filters=new FlowLayoutPanel{Dock=DockStyle.Top,Height=42};filters.Controls.Add(filter);filters.Controls.Add(query);
            var controls=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=48,FlowDirection=FlowDirection.RightToLeft};
            var close=new Button{Text="Fechar",DialogResult=DialogResult.Cancel};var export=new Button{Text="Salvar relatório TXT"};var prepare=new Button{Text="Preparar operação",Enabled=false};Style(close);Style(export);Style(prepare);
            var guidance=new Label{Dock=DockStyle.Bottom,Height=44,ForeColor=Muted,Text="Selecione uma ocorrência para ver se há uma operação assistida."};
            bool allowed=Diagnostics.CanPrepare(findings);
            Finding? Selected()=>list.CurrentRow?.Tag as Finding;
            void UpdateAction(){
                var item=Selected();var action=item==null?DiagnosticAction.None:Diagnostics.ActionFor(item);
                prepare.Enabled=allowed&&action!=DiagnosticAction.None;
                prepare.Text=action==DiagnosticAction.RepairMods?"Preparar reinstalação":action==DiagnosticAction.ManageMods?"Abrir catálogo de mods":"Preparar operação";
                guidance.Text=!allowed?"Há bloqueios de segurança. Resolva os erros/avisos impeditivos e execute novo diagnóstico.":action==DiagnosticAction.None?"Este item exige revisão manual; não existe correção automática segura.":"O atalho apenas prepara uma nova análise; backup e duas confirmações continuam obrigatórios.";
            }
            void Populate(){
                list.Rows.Clear();
                foreach(var f in findings){
                    if(filter.SelectedIndex>0&&(int)f.Level!=filter.SelectedIndex-1)continue;
                    if(!string.Join(" ",f.Code,f.Scope,f.Problem,f.Advice).Contains(query.Text,StringComparison.OrdinalIgnoreCase))continue;
                    int row=list.Rows.Add(f.Level==Severity.Error?"Erro":f.Level==Severity.Warning?"Aviso":"Informação",f.Scope,f.Problem,f.Advice);list.Rows[row].Tag=f;
                }
                UpdateAction();
            }
            filter.SelectedIndexChanged+=(_,_)=>Populate();query.TextChanged+=(_,_)=>Populate();list.SelectionChanged+=(_,_)=>UpdateAction();
            prepare.Click+=(_,_)=>{var f=Selected();if(f==null||!allowed)return;next=Diagnostics.ActionFor(f);if(next!=DiagnosticAction.None)dialog.DialogResult=DialogResult.OK;};
            Populate();
            export.Click+=(_,_)=>{using var save=new SaveFileDialog{Filter="Relatório de texto (*.txt)|*.txt",FileName="Diagnostico-Dedicado-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".txt",OverwritePrompt=false};if(save.ShowDialog(dialog)==DialogResult.OK)try{
                Engine.SafePath(save.FileName);
                using var stream=new FileStream(save.FileName,FileMode.CreateNew,FileAccess.Write,FileShare.None);
                using var writer=new StreamWriter(stream,Encoding.UTF8);
                foreach(var line in new[]{"Gerenciador de Dedicado 1.0.0","Diagnóstico limitado e somente leitura; sem tokens ou linhas brutas de log."}.Concat(findings.Select(f=>$"[{f.Level}] {f.Code} | {f.Scope}\n{f.Problem}\n{f.Advice}\n")))writer.WriteLine(line);
            }catch(Exception e){MessageBox.Show(dialog,e.Message,"Falha ao salvar");}};

            controls.Controls.Add(close);controls.Controls.Add(export);controls.Controls.Add(prepare);dialog.Controls.Add(list);dialog.Controls.Add(filters);dialog.Controls.Add(header);dialog.Controls.Add(guidance);dialog.Controls.Add(controls);dialog.CancelButton=close;dialog.ShowDialog(this);
            status.Text="Diagnóstico concluído. Nenhuma alteração no dedicado.";
        }catch(Exception e){next=DiagnosticAction.None;Write("Diagnóstico não concluído: "+e.Message);status.Text="Diagnóstico incompleto.";}finally{Busy(false);}
        if(next!=DiagnosticAction.None){
            try{
                // Bind the shortcut to the exact diagnosed directory, not to its displayed number.
                var expected=Path.TrimEndingDirectorySeparator(Path.GetFullPath(cluster));
                var fresh=Engine.LoadClusterDirectory(expected);
                if(!fresh.Path.Equals(expected,StringComparison.OrdinalIgnoreCase))throw new IOException("Destino do diagnóstico divergente.");
                Engine.Offline();
                exclusive.Checked=false;mode.SelectedIndex=next==DiagnosticAction.RepairMods?1:2;
                destinationRoot.Text=expected;RefreshClusters();
                var index=target.Items.Cast<Cluster>().ToList().FindIndex(c=>c.Path.Equals(expected,StringComparison.OrdinalIgnoreCase));
                if(index<0)throw new IOException("O cluster diagnosticado não está mais disponível.");
                target.SelectedIndex=index;
                Write("Operação assistida: preparando nova análise para "+fresh.Name+". Nenhum reset autorizado.");
                await Analyze();
            }catch(Exception e){InvalidatePlan();Write("Operação assistida bloqueada: "+e.Message);}
        }
    }
    async Task Analyze(){
        InvalidatePlan();

        if(target.SelectedItem is not Cluster b||(!InPlace&&source.SelectedItem is not Cluster)){Write("Selecione os clusters exigidos pelo modo.");return;}
        var a=InPlace?b:(Cluster)source.SelectedItem!;
        var installation=install.Text;bool lookup=online.Checked;var selectedMode=mode.SelectedIndex;Busy(true);
        try{
            status.Text="Lendo configurações e validando caminhos...";
            var plan=await Task.Run(()=>{var dest=Engine.LoadClusterDirectory(b.Path);return selectedMode==0?Engine.Analyze(Engine.LoadClusterDirectory(a.Path),dest,installation):selectedMode==1?Engine.AnalyzeRepair(dest,installation):Engine.AnalyzeEditBaseline(dest,installation);});
            if(selectedMode>=2){
                var edits=selectedMode==2?EditVisual(plan,lookup):EditOverrides(plan);
                if(edits==null){status.Text="Edição cancelada. Nenhum arquivo alterado.";return;}
                var baseline=plan.Fingerprint;var dest=plan.Target;
                plan=await Task.Run(()=>Engine.AnalyzeRepair(Engine.LoadClusterDirectory(dest.Path),installation,edits,baseline));
            }
            await ShowPlan(plan,lookup);
        }catch(Exception e){Write("Análise bloqueada: "+e.Message);status.Text="Análise falhou.";}finally{Busy(false);}
    }
    async Task ShowPlan(Plan plan,bool lookup){
            var ids=plan.Shards.SelectMany(s=>s.Wanted.Concat(s.Old)).Distinct().ToArray();
            status.Text="Consultando nomes dos mods...";
            var names=await Names(ids,plan,lookup);
            foreach(var s in plan.Shards)foreach(var id in s.Wanted.Concat(s.Old).Distinct().Order()){
                var action=s.Wanted.Contains(id)?s.Old.Contains(id)?(plan.Edited?"Reinstalar / opções editadas":plan.InPlace?"Reinstalar / manter opções":"Reinstalar / copiar opções"):"Adicionar":"Remover da configuração";
                grid.Rows.Add(names.GetValueOrDefault(id,"Nome não identificado"),id,s.Name,action);
            }
            foreach(var warning in Engine.ModWarnings(plan,publishedDependencies))Write(warning);
            current=plan;
            status.Text=plan.Ids.Length+" mods únicos ativos • "+(plan.InPlace?plan.Target.Name:plan.Source.Name+" → "+plan.Target.Name);
            Write("Origem: "+plan.Source.Path+" — "+plan.Source.Name);Write("Destino: "+plan.Target.Path+" — "+plan.Target.Name);Write("Instalação: "+plan.Install);
            Write("Simulação concluída. Nenhum arquivo alterado. Revise a lista e confirme instalação exclusiva.");
            if(names.Values.Any(n=>n=="Nome não identificado"))Write("Alguns nomes não puderam ser consultados. Confira os IDs na Workshop.");
    }
    Dictionary<string,byte[]>? EditVisual(Plan plan,bool lookup){
        using var dialog=new Form{Text="Catálogo de mods — "+plan.Target.Name,Size=new Size(1000,650),MinimumSize=new Size(760,520),StartPosition=FormStartPosition.CenterParent,BackColor=Bg,ForeColor=Ink,Font=Font};
        var utf8=new UTF8Encoding(false,true);
        var texts=plan.Shards.ToDictionary(s=>s.Name,s=>utf8.GetString(s.Content).TrimStart('\uFEFF'));
        var names=new Dictionary<string,string>();var previews=new Dictionary<string,string>();
        var shard=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=105,BackColor=PanelColor,ForeColor=Ink};
        shard.Items.AddRange(plan.Target.Shards.Cast<object>().ToArray());shard.SelectedIndex=0;
        var input=new TextBox{Width=270,PlaceholderText="ID ou link HTTPS da Workshop",BackColor=PanelColor,ForeColor=Ink};
        Button add=new(){Text="Adicionar / ativar"},toggle=new(){Text="Ativar / desativar"},refresh=new(){Text="Consultar Workshop"},open=new(){Text="Abrir Workshop"},ok=new(){Text="Revisar plano"},cancel=new(){Text="Cancelar",DialogResult=DialogResult.Cancel};
        var options=new Button{Text="Editar opções"};
        var browseWorkshop=new Button{Text="Pesquisar na Workshop"};
        foreach(var b in new[]{add,toggle,options,refresh,open,browseWorkshop,ok,cancel})Style(b);
        var search=new TextBox{Width=200,MaxLength=120,PlaceholderText="Filtrar ou pesquisar por nome",BackColor=PanelColor,ForeColor=Ink};
        var message=new Label{Dock=DockStyle.Bottom,Height=38,ForeColor=Muted,Text="Alterações em memória. Selecione um shard e revise antes de aplicar."};
        var table=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,BackgroundColor=PanelColor,EnableHeadersVisualStyles=false};
        table.ColumnHeadersDefaultCellStyle.BackColor=Raised;table.ColumnHeadersDefaultCellStyle.ForeColor=Ink;table.DefaultCellStyle.BackColor=Bg;table.DefaultCellStyle.ForeColor=Ink;table.DefaultCellStyle.SelectionBackColor=Raised;table.DefaultCellStyle.SelectionForeColor=Accent;
        foreach(var h in new[]{"Mod","ID","Estado"})table.Columns.Add(h,h);table.Columns[0].FillWeight=180;
        var preview=new PictureBox{Dock=DockStyle.Top,Height=210,BackColor=PanelColor,SizeMode=PictureBoxSizeMode.Zoom};
        var title=new Label{Dock=DockStyle.Top,Height=74,ForeColor=Ink,Text="Selecione um mod para visualizar."};
        var panel=new Panel{Dock=DockStyle.Right,Width=250,Padding=new Padding(8),BackColor=PanelColor};panel.Controls.Add(title);panel.Controls.Add(preview);
        var controls=new FlowLayoutPanel{Dock=DockStyle.Top,Height=78,Padding=new Padding(4),AutoScroll=true};
        controls.Controls.AddRange(new Control[]{shard,input,add,toggle,options,search,browseWorkshop,refresh,open});
        var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=44,FlowDirection=FlowDirection.RightToLeft};actions.Controls.Add(cancel);actions.Controls.Add(ok);
        dialog.Controls.Add(table);dialog.Controls.Add(panel);dialog.Controls.Add(message);dialog.Controls.Add(actions);dialog.Controls.Add(controls);dialog.CancelButton=cancel;
        bool loading=false;int previewGeneration=0;using var lifetime=new CancellationTokenSource();CancellationTokenSource? activePreview=null;
        string Shard()=>shard.SelectedItem!.ToString()!;
        string? Selected()=>table.CurrentRow?.Cells[1].Value as string;
        void ClearImage(){var old=preview.Image;preview.Image=null;old?.Dispose();}
        void Populate(string? select=null){
            table.Rows.Clear();
            foreach(var (id,enabled) in Lua.Entries(texts[Shard()]).OrderBy(x=>names.GetValueOrDefault(x.Key,x.Key),StringComparer.OrdinalIgnoreCase)){
                var name=names.GetValueOrDefault(id,"Nome não consultado");
                if(!name.Contains(search.Text,StringComparison.OrdinalIgnoreCase)&&!id.Contains(search.Text,StringComparison.OrdinalIgnoreCase))continue;
                var index=table.Rows.Add(name,id,enabled?"Ativo":"Desativado");if(id==select)table.CurrentCell=table.Rows[index].Cells[0];
            }
        }
        async Task Lookup(){
            if(loading)return;loading=true;refresh.Enabled=add.Enabled=toggle.Enabled=ok.Enabled=false;
            try{
                var all=texts.Values.SelectMany(t=>Lua.Entries(t).Keys).Distinct().ToArray();
                message.Text="Consultando dados públicos da Workshop...";
                var updated=new Dictionary<string,string>();
                var found=await Names(all,plan,lookup,updated);
                if(lifetime.IsCancellationRequested||dialog.IsDisposed)return;
                foreach(var (id,name) in found)names[id]=name;
                foreach(var id in all)previews.Remove(id);
                foreach(var (id,url) in updated)previews[id]=url;
                Populate(Selected());message.Text="Mods listados. Imagens originais aparecem ao selecionar um mod.";
            }catch(Exception e){if(!dialog.IsDisposed)message.Text="Consulta indisponível: "+e.Message;}
            finally{loading=false;if(!dialog.IsDisposed)refresh.Enabled=add.Enabled=toggle.Enabled=ok.Enabled=true;}
        }
        async Task ShowPreview(){
            var generation=++previewGeneration;activePreview?.Cancel();activePreview?.Dispose();activePreview=null;ClearImage();var id=Selected();title.Text=id==null?"Selecione um mod":names.GetValueOrDefault(id,id)+"\nWorkshop "+id;
            if(id==null||!previews.TryGetValue(id,out var url))return;
            Image? img=null;
            var request=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);activePreview=request;
            try{img=await PreviewImage.Fetch(url,request.Token);
                if(generation==previewGeneration&&!dialog.IsDisposed&&!lifetime.IsCancellationRequested){preview.Image=img;img=null;}
            }catch{}finally{img?.Dispose();if(ReferenceEquals(activePreview,request)){activePreview=null;request.Dispose();}}
        }
        void Edit(string id,bool enabled){
            try{texts[Shard()]=Lua.SetEnabled(texts[Shard()],id,enabled);Populate(id);message.Text="Alteração preparada; nenhum arquivo gravado.";}
            catch(Exception e){MessageBox.Show(dialog,e.Message,"Configuração inválida");}
        }
        add.Click+=async(_,_)=>{if(!WorkshopId.TryParse(input.Text,out var id)){message.Text="Use um ID numérico válido ou link HTTPS oficial.";return;}Edit(id,true);input.Clear();await Lookup();};
        toggle.Click+=(_,_)=>{var id=Selected();if(id!=null&&Lua.Entries(texts[Shard()]).TryGetValue(id,out var enabled))Edit(id,!enabled);};
        options.Click+=(_,_)=>{
            if(Selected() is not string id)return;
            var selectedShard=Shard();
            var original=texts[selectedShard];
            try{
                var declared=Array.Empty<DeclaredModOption>();
                foreach(var path in Engine.ModInfoPaths(plan,selectedShard,id)){
                    try{Engine.SafePath(path);if(File.Exists(path)&&new FileInfo(path).Length<=1_000_000)declared=Lua.DeclaredOptions(File.ReadAllText(path));if(declared.Length>0)break;}
                    catch(IOException){}catch(UnauthorizedAccessException){}
                }
                var edited=EditModOptions(dialog,original,id,selectedShard,declared);
                if(edited==original)return;
                texts[selectedShard]=edited;
                message.Text="Opções preparadas. Revise o plano para continuar.";
            }catch(FormatException error){MessageBox.Show(dialog,error.Message,"Configuração inválida");}
        };
        shard.SelectedIndexChanged+=(_,_)=>Populate();search.TextChanged+=(_,_)=>Populate(Selected());
        table.SelectionChanged+=async(_,_)=>await ShowPreview();
        refresh.Click+=async(_,_)=>await Lookup();
        browseWorkshop.Click+=async(_,_)=>{
            if(loading)return;
            var selectedShard=Shard();
            var hit=BrowseWorkshop(dialog,search.Text,selectedShard);
            if(hit==null||Shard()!=selectedShard)return;
            if(hit.PublishedDependencies!=null)publishedDependencies[hit.Id]=hit.PublishedDependencies; Edit(hit.Id,true);await Lookup();
        };
        open.Click+=(_,_)=>{var id=Selected();Open(id==null?"https://steamcommunity.com/app/322330/workshop/":"https://steamcommunity.com/sharedfiles/filedetails/?id="+id);};
        Dictionary<string,byte[]>? result=null;
        ok.Click+=(_,_)=>{try{var candidate=new Dictionary<string,byte[]>();foreach(var sh in plan.Shards){var text=texts[sh.Name];Lua.Active(text);candidate[sh.Name]=text==utf8.GetString(sh.Content).TrimStart('\uFEFF')?sh.Content:utf8.GetBytes(text);}
            if(!candidate.Values.Any(b=>Lua.Active(utf8.GetString(b).TrimStart('\uFEFF')).Count>0)&&MessageBox.Show(dialog,"Todos os mods ficarão desativados. Entidades dependentes de mods podem desaparecer ao iniciar o mundo. Prosseguir apenas para revisão?","Lista vazia",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;
            result=candidate;dialog.DialogResult=DialogResult.OK;}catch(Exception e){message.Text=e.Message;}};
        dialog.FormClosing+=(_,e)=>{if(loading){e.Cancel=true;message.Text="A consulta está em andamento.";}else{lifetime.Cancel();activePreview?.Cancel();activePreview?.Dispose();activePreview=null;++previewGeneration;ClearImage();}};
        Populate();dialog.Shown+=async(_,_)=>await Lookup();
        return dialog.ShowDialog(this)==DialogResult.OK?result:null;
    }
    Dictionary<string,byte[]>? EditOverrides(Plan plan){
        using var dialog=new Form{Text="Editar mods — "+plan.Target.Name,Size=new Size(820,570),MinimumSize=new Size(580,380),StartPosition=FormStartPosition.CenterParent,BackColor=Bg,ForeColor=Ink,Font=Font};
        var tabs=new TabControl{Dock=DockStyle.Fill};var editors=new Dictionary<string,(TextBox Box,string Initial)>();
        var utf8=new UTF8Encoding(false,true);
        foreach(var shard in plan.Shards){
            var page=new TabPage(shard.Name){BackColor=PanelColor,ForeColor=Ink};
            var box=new TextBox{Dock=DockStyle.Fill,Multiline=true,AcceptsReturn=true,AcceptsTab=true,ScrollBars=ScrollBars.Both,WordWrap=false,MaxLength=4_000_000,BackColor=PanelColor,ForeColor=Ink,Font=new Font("Consolas",10)};
            box.Text=utf8.GetString(shard.Content).TrimStart('\uFEFF');editors.Add(shard.Name,(box,box.Text));page.Controls.Add(box);tabs.TabPages.Add(page);
        }
        var hint=new Label{Dock=DockStyle.Top,Height=76,Text="Edite apenas a tabela literal return { ... }. Para incluir um mod, adicione uma entrada ['workshop-ID']={enabled=true,configuration_options={}}, dentro da tabela. Para desativar, use enabled=false. Nada será gravado antes da análise e das duas confirmações."};
        var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=46,FlowDirection=FlowDirection.RightToLeft};
        var ok=new Button{Text="Validar edição"};var cancel=new Button{Text="Cancelar",DialogResult=DialogResult.Cancel};Style(ok);Style(cancel);
        Dictionary<string,byte[]>? result=null;
        ok.Click+=(_,_)=>{try{var candidate=new Dictionary<string,byte[]>();foreach(var shard in plan.Shards){var edit=editors[shard.Name];var data=edit.Box.Text==edit.Initial?shard.Content: utf8.GetBytes(edit.Box.Text);if(data.Length>4_000_000)throw new FormatException("Arquivo grande demais.");Lua.Active(utf8.GetString(data).TrimStart('\uFEFF'));candidate.Add(shard.Name,data);}result=candidate;dialog.DialogResult=DialogResult.OK;}catch(Exception e){MessageBox.Show(dialog,e.Message,"Configuração inválida",MessageBoxButtons.OK,MessageBoxIcon.Warning);}};
        actions.Controls.Add(cancel);actions.Controls.Add(ok);dialog.Controls.Add(tabs);dialog.Controls.Add(hint);dialog.Controls.Add(actions);dialog.CancelButton=cancel;
        return dialog.ShowDialog(this)==DialogResult.OK?result:null;
    }
    static async Task<Dictionary<string,string>> Names(string[] ids,Plan plan,bool online,Dictionary<string,string>? previews=null){
        var result=new Dictionary<string,string>();
        foreach(var id in ids){
            var paths=plan.Target.Shards.SelectMany(shard=>Engine.ModInfoPaths(plan,shard,id)).Distinct();
            foreach(var p in paths)try{if(File.Exists(p)&&new FileInfo(p).Length<1_000_000){Engine.SafePath(p);var m=Regex.Match(File.ReadAllText(p),"""(?m)^\s*name\s*=\s*["']([^"'\r\n]+)["']""");if(m.Success){result[id]=m.Groups[1].Value+" (local)";break;}}}catch{}
        }
        if(online)try{
            using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(12)};
            using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(25));
            foreach(var chunk in ids.Chunk(50)){
                var data=new Dictionary<string,string>{{"itemcount",chunk.Length.ToString()}};for(int i=0;i<chunk.Length;i++)data["publishedfileids["+i+"]"]=chunk[i];
                using var response=await http.PostAsync("https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/",new FormUrlEncodedContent(data),limit.Token);response.EnsureSuccessStatusCode();
                using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                foreach(var item in json.RootElement.GetProperty("response").GetProperty("publishedfiledetails").EnumerateArray()){
                    var id=item.GetProperty("publishedfileid").GetString();if(id==null||!chunk.Contains(id))continue;
                    if(item.TryGetProperty("result",out var r)&&r.GetInt32()==1&&item.TryGetProperty("title",out var title)&&item.TryGetProperty("consumer_app_id",out var app)&&app.GetInt32()==322330){
                        result[id]=(title.GetString()??"Nome não identificado").Replace("\r"," ").Replace("\n"," ");
                        if(previews!=null&&item.TryGetProperty("preview_url",out var preview)&&preview.GetString() is string url&&PreviewImage.Allowed(url))previews[id]=url;
                    }
                    else result[id]="Workshop indisponível / privada";
                }
            }
        }catch{}
        foreach(var id in ids)result.TryAdd(id,"Nome não identificado");return result;
    }
    async Task Reset(){
        var p=current;if(p==null||!exclusive.Checked)return;
        if(MessageBox.Show((p.InPlace?(p.Edited?"Modo: edição e reinstalação\n":"Modo: reinstalação sem alterar opções\n"):"Modo: copiar outro mundo\n")+"Origem: "+p.Source.Name+" ("+p.Source.FolderName+")\n"+p.Source.Path+"\n\n"+p.Ids.Length+" mods únicos. A lista na tela corresponde ao plano.\nConfirma esta configuração?","Confirmação 1 de 2",MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;
        if(grid.Rows.Cast<DataGridViewRow>().Any(r=>Convert.ToString(r.Cells[0].Value) is ("Workshop indisponível / privada" or "Nome não identificado")&&Convert.ToString(r.Cells[3].Value)!="Remover da configuração")){Write("Mod indisponível ou sem nome identificado na origem: reset bloqueado. Ative a consulta Workshop e analise novamente.");return;}
        using var dialog=new Form{Text="Confirmação 2 de 2",Size=new Size(650,350),StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,BackColor=Bg,ForeColor=Ink,Font=new Font("Segoe UI",10)};
        var layout=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(15)};
        var phrase=(p.Ids.Length==0?"DESATIVAR TODOS ":"RESET ")+p.Target.FolderName;
        var label=new Label{Text=(p.Ids.Length==0?"ATENÇÃO: TODOS OS MODS SERÃO DESATIVADOS.\n":"")+"Destino: "+p.Target.Name+"\n"+p.Target.Path+"\n\nSomente caches V1 dos IDs afetados e UGC do destino serão movidos.\nMods removidos podem afetar entidades ao iniciar o mundo.\nDigite exatamente: "+phrase,AutoSize=true,MaximumSize=new Size(570,0)};
        var input=new TextBox{Width=450,BackColor=PanelColor,ForeColor=Ink,BorderStyle=BorderStyle.FixedSingle};var confirm=new Button{Text="Executar",Enabled=false,DialogResult=DialogResult.OK};var cancel=new Button{Text="Cancelar",DialogResult=DialogResult.Cancel};input.TextChanged+=(_,_)=>confirm.Enabled=input.Text==phrase;
        Style(confirm);Style(cancel);layout.Controls.Add(label);layout.Controls.Add(input);layout.Controls.Add(confirm);layout.Controls.Add(cancel);dialog.Controls.Add(layout);dialog.CancelButton=cancel;dialog.AcceptButton=confirm;
        if(dialog.ShowDialog(this)!=DialogResult.OK||input.Text!=phrase)return;
        Busy(true);try{status.Text="Manutenção em andamento";var result=await Task.Run(()=>Engine.Apply(p,Write));Write("Atualizadores concluídos. Backup: "+result);Write("Confira os logs e teste a entrada no mundo e nas cavernas. Não reiniciamos o servidor.");status.Text="Manutenção concluída; validação do mundo pendente.";}catch(Exception e){Write(e.Message);status.Text="Falha: leia o relatório antes de iniciar o mundo.";}finally{current=null;Busy(false);}
    }
    static void Open(string url){try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}catch(Exception e){MessageBox.Show(e.Message);}}
    async Task OpenBackups(){
        if(busy)return;
        var rootPath=Recovery.DefaultRoot;Directory.CreateDirectory(rootPath);
        try{if(InterruptedMaintenance.Pending(rootPath).Length>0){await RecoverInterrupted(rootPath);return;}}catch(Exception ex){Write("Recuperação: "+ex.Message);return;}
        using var dialog=new Form{Text="Central de backups — configurações de mods",Size=new Size(850,480),MinimumSize=new Size(620,360),StartPosition=FormStartPosition.CenterParent,BackColor=Bg,ForeColor=Ink,Font=Font};
        var hint=new Label{Dock=DockStyle.Top,Height=82,ForeColor=Muted,Text="Recupere configurações de mods de backups com manifesto de integridade. Selecione primeiro o mesmo cluster e instalação na janela principal. Saves, tokens e server.ini atuais NÃO serão restaurados. Backups antigos podem ser abertos na pasta."};
        var list=new ListBox{Dock=DockStyle.Fill,BackColor=PanelColor,ForeColor=Ink,HorizontalScrollbar=true};
        try{list.Items.AddRange(Recovery.List(rootPath).Cast<object>().ToArray());}catch(Exception e){Write("Backups: "+e.Message);return;}
        var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=48,FlowDirection=FlowDirection.RightToLeft};
        var close=new Button{Text="Fechar",DialogResult=DialogResult.Cancel};var folder=new Button{Text="Abrir pasta"};var prepare=new Button{Text="Preparar recuperação",Enabled=false};
        foreach(var button in new[]{close,folder,prepare})Style(button);
        list.SelectedIndexChanged+=(_,_)=>prepare.Enabled=list.SelectedItem is string&&target.SelectedItem is Cluster;
        folder.Click+=(_,_)=>Open(rootPath);
        prepare.Click+=(_,_)=>dialog.DialogResult=DialogResult.OK;
        actions.Controls.Add(close);actions.Controls.Add(prepare);actions.Controls.Add(folder);
        dialog.Controls.Add(list);dialog.Controls.Add(hint);dialog.Controls.Add(actions);dialog.CancelButton=close;
        if(dialog.ShowDialog(this)!=DialogResult.OK||list.SelectedItem is not string backup||target.SelectedItem is not Cluster chosen)return;
        var installation=install.Text;var lookup=online.Checked;
        mode.SelectedIndex=1;destinationRoot.Text=chosen.Path;RefreshClusters();
        var index=target.Items.Cast<Cluster>().ToList().FindIndex(x=>x.Path.Equals(chosen.Path,StringComparison.OrdinalIgnoreCase));
        if(index<0){Write("Cluster original não encontrado.");return;}
        target.SelectedIndex=index;InvalidatePlan();exclusive.Checked=false;Busy(true);
        try{
            status.Text="Conferindo backup e destino...";
            var plan=await Task.Run(()=>{var fresh=Engine.LoadClusterDirectory(chosen.Path);return Recovery.Prepare(backup,fresh,installation);});
            Write("Recuperação preparada a partir de "+Path.GetFileName(backup)+". Apenas configurações de mods; nenhuma gravação efetuada.");
            await ShowPlan(plan,lookup);
        }catch(Exception e){InvalidatePlan();Write("Recuperação bloqueada: "+e.Message);}finally{Busy(false);}
    }
}
