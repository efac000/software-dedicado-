using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Dedicado;
namespace Gerenciador;
static class Program {
    [STAThread] static int Main(string[] args){
        if(args.Length==1&&args[0]=="--self-test")return Engine.ValidNumber("1")&&!Engine.ValidNumber("1a")&&Lua.Active("return {}").Count==0?0:1;
        ApplicationConfiguration.Initialize();Application.Run(new MainForm());return 0;
    }
}
sealed class MainForm:Form{
    static readonly Color Bg=Color.FromArgb(22,27,35), PanelColor=Color.FromArgb(33,41,52), Ink=Color.FromArgb(231,237,244), Accent=Color.FromArgb(100,210,171);
    readonly TextBox root=new(),destinationRoot=new(),install=new(),log=new(){Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical};
    readonly ComboBox source=new(){DropDownStyle=ComboBoxStyle.DropDownList},target=new(){DropDownStyle=ComboBoxStyle.DropDownList};
    readonly DataGridView grid=new(){ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
    readonly CheckBox exclusive=new(){Text="Confirmo: instalação exclusiva, UGC padrão, servidor e reinício automático desligados.",AutoSize=true};
    readonly CheckBox online=new(){Text="Consultar nomes na Steam Workshop (envia somente IDs públicos).",Checked=true,AutoSize=true};
    readonly Button scan=new(){Text="Localizar clusters"},analyze=new(){Text="Analisar (sem alterações)"},reset=new(){Text="Confirmar e reinstalar",Enabled=false},backups=new(){Text="Abrir backups"},website=new(){Text="Site / atualizações"};
    readonly Label status=new(){Text="Selecione as pastas e os mundos.",AutoSize=true};
    Plan? current; bool busy;
    public MainForm(){
        Text="Gerenciador de Dedicado | 1.0.0";MinimumSize=new Size(780,620);Size=new Size(1100,830);AutoScaleMode=AutoScaleMode.Dpi;StartPosition=FormStartPosition.CenterScreen;BackColor=Bg;ForeColor=Ink;Font=new Font("Segoe UI",10);
        var main=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(18),ColumnCount=1,RowCount=11,BackColor=Bg};
        main.RowStyles.Add(new RowStyle(SizeType.Absolute,70));main.RowStyles.Add(new RowStyle(SizeType.Absolute,45));main.RowStyles.Add(new RowStyle(SizeType.Absolute,45));main.RowStyles.Add(new RowStyle(SizeType.Absolute,45));main.RowStyles.Add(new RowStyle(SizeType.Absolute,45));main.RowStyles.Add(new RowStyle(SizeType.Absolute,37));main.RowStyles.Add(new RowStyle(SizeType.Percent,62));main.RowStyles.Add(new RowStyle(SizeType.Absolute,40));main.RowStyles.Add(new RowStyle(SizeType.Percent,38));main.RowStyles.Add(new RowStyle(SizeType.Absolute,38));main.RowStyles.Add(new RowStyle(SizeType.Absolute,35));
        Controls.Add(main);
        var heading=new Label{Text="GERENCIADOR DE DEDICADO\n1.0.0  •  Don't Starve Together  /  Windows",Dock=DockStyle.Fill,Font=new Font("Segoe UI",16,FontStyle.Bold),ForeColor=Accent};
        main.Controls.Add(heading);
        main.Controls.Add(PathRow("Clusters de origem",root));
        main.Controls.Add(PathRow("Clusters de destino",destinationRoot));
        main.Controls.Add(PathRow("Instalação dedicada",install));
        var worlds=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=5};worlds.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,75));worlds.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));worlds.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,75));worlds.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));worlds.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,150));
        worlds.Controls.Add(new Label{Text="Origem",AutoSize=true,Anchor=AnchorStyles.Left});worlds.Controls.Add(source);worlds.Controls.Add(new Label{Text="Destino",AutoSize=true,Anchor=AnchorStyles.Left});worlds.Controls.Add(target);worlds.Controls.Add(scan);
        source.Dock=target.Dock=DockStyle.Fill;main.Controls.Add(worlds);
        var choices=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true};choices.Controls.Add(online);choices.Controls.Add(analyze);main.Controls.Add(choices);
        grid.Dock=DockStyle.Fill;grid.BackgroundColor=PanelColor;grid.BorderStyle=BorderStyle.None;grid.EnableHeadersVisualStyles=false;grid.ColumnHeadersDefaultCellStyle.BackColor=PanelColor;grid.ColumnHeadersDefaultCellStyle.ForeColor=Ink;grid.DefaultCellStyle.BackColor=Bg;grid.DefaultCellStyle.ForeColor=Ink;grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(43,80,78);grid.DefaultCellStyle.SelectionForeColor=Ink;grid.RowTemplate.Height=30;
        foreach(var h in new[]{"Mod / Workshop","ID","Shard","Alteração"})grid.Columns.Add(h,h);
        grid.Columns[0].FillWeight=180;main.Controls.Add(grid);main.Controls.Add(exclusive);
        log.Dock=DockStyle.Fill;log.BackColor=PanelColor;log.ForeColor=Ink;main.Controls.Add(log);
        var actions=new FlowLayoutPanel{Dock=DockStyle.Fill};actions.Controls.Add(reset);actions.Controls.Add(backups);actions.Controls.Add(website);main.Controls.Add(actions);main.Controls.Add(status);
        foreach(var b in new[]{scan,analyze,reset,backups,website})Style(b);
        root.Text=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Klei","DoNotStarveTogether");
        destinationRoot.Text=root.Text;
        install.Text=FindInstall();
        root.TextChanged+=(_,_)=>{InvalidatePlan();source.Items.Clear();};destinationRoot.TextChanged+=(_,_)=>{InvalidatePlan();target.Items.Clear();};install.TextChanged+=(_,_)=>InvalidatePlan();source.SelectedIndexChanged+=(_,_)=>InvalidatePlan();target.SelectedIndexChanged+=(_,_)=>InvalidatePlan();exclusive.CheckedChanged+=(_,_)=>reset.Enabled=!busy&&current!=null&&exclusive.Checked;
        scan.Click+=(_,_)=>RefreshClusters();analyze.Click+=async(_,_)=>await Analyze();reset.Click+=async(_,_)=>await Reset();backups.Click+=(_,_)=>OpenBackups();website.Click+=(_,_)=>Open("https://github.com/efac000/software-dedicado-/releases");
        FormClosing+=(_,e)=>{if(busy){e.Cancel=true;MessageBox.Show("A operação está em andamento. Aguarde a conclusão.");}};
        Shown+=(_,_)=>RefreshClusters();
    }
    static void Style(Button b){b.AutoSize=true;b.FlatStyle=FlatStyle.Flat;b.BackColor=PanelColor;b.ForeColor=Ink;b.FlatAppearance.BorderColor=Color.FromArgb(65,78,95);b.Padding=new Padding(5,0,5,0);}
    Control PathRow(string title,TextBox box){
        var row=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3};row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,150));row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));
        row.Controls.Add(new Label{Text=title,AutoSize=true,Anchor=AnchorStyles.Left});box.Dock=DockStyle.Fill;box.BackColor=PanelColor;box.ForeColor=Ink;row.Controls.Add(box);var b=new Button{Text="Procurar"};Style(b);b.Click+=(_,_)=>{using var d=new FolderBrowserDialog{Description=title};if(Directory.Exists(box.Text))d.SelectedPath=box.Text;if(d.ShowDialog()==DialogResult.OK){box.Text=d.SelectedPath;if(box==root||box==destinationRoot)RefreshClusters();}};row.Controls.Add(b);return row;
    }
    static string FindInstall(){
        var candidates=new List<string>();
        foreach(var key in new[]{@"HKEY_CURRENT_USER\Software\Valve\Steam",@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam"}){
            try{if(Registry.GetValue(key,"SteamPath",null) is string s)candidates.Add(s);if(Registry.GetValue(key,"InstallPath",null) is string p)candidates.Add(p);}catch{}
        }
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Steam"));
        foreach(var steam in candidates.ToArray())try{var vdf=Path.Combine(steam,"steamapps","libraryfolders.vdf");if(File.Exists(vdf))foreach(Match m in Regex.Matches(File.ReadAllText(vdf),""" "path"\s+"([^"]+)" """.Trim()))candidates.Add(m.Groups[1].Value.Replace(@"\\",@"\"));}catch{}
        foreach(var library in candidates.Distinct(StringComparer.OrdinalIgnoreCase)){
            var p=Path.Combine(library,"steamapps","common","Don't Starve Together Dedicated Server");try{Engine.FindExe(p);return p;}catch{}
        }
        return "";
    }
    void InvalidatePlan(){current=null;reset.Enabled=false;grid.Rows.Clear();status.Text="Analise novamente antes de executar.";}
    void RefreshClusters(){
        InvalidatePlan();source.Items.Clear();target.Items.Clear();
        try{foreach(var c in Engine.Discover(root.Text))source.Items.Add(c);foreach(var c in Engine.Discover(destinationRoot.Text))target.Items.Add(c);status.Text=$"Origem: {source.Items.Count} | Destino: {target.Items.Count} clusters. Selecione os dois mundos.";if(source.Items.Count==0||target.Items.Count==0)status.Text+=" Confira as pastas ou selecione-as manualmente.";}
        catch(Exception e){Write(e.Message);}
    }
    void Write(string s){if(InvokeRequired){BeginInvoke(new Action(()=>Write(s)));return;}log.AppendText(DateTime.Now.ToString("HH:mm:ss")+"  "+s+Environment.NewLine);}
    void Busy(bool b){
        busy=b;root.Enabled=destinationRoot.Enabled=install.Enabled=source.Enabled=target.Enabled=scan.Enabled=analyze.Enabled=exclusive.Enabled=online.Enabled=!b;
        // Disable path-picker buttons as well.
        foreach(Control row in Controls[0].Controls)if(row is TableLayoutPanel)row.Enabled=!b;
        reset.Enabled=!b&&current!=null&&exclusive.Checked;
    }
    async Task Analyze(){
        InvalidatePlan();
        if(source.SelectedItem is not Cluster a||target.SelectedItem is not Cluster b){Write("Selecione dois clusters existentes.");return;}
        var installation=install.Text;bool lookup=online.Checked;Busy(true);
        try{
            var plan=await Task.Run(()=>Engine.Analyze(Engine.Load(Path.GetDirectoryName(a.Path)!,a.Number),Engine.Load(Path.GetDirectoryName(b.Path)!,b.Number),installation));
            var ids=plan.Shards.SelectMany(s=>s.Wanted.Concat(s.Old)).Distinct().ToArray();
            var names=await Names(ids,plan.Install,lookup);
            foreach(var s in plan.Shards)foreach(var id in s.Wanted.Concat(s.Old).Distinct().Order()){
                var action=s.Wanted.Contains(id)?s.Old.Contains(id)?"Reinstalar / copiar opções":"Adicionar":"Remover da configuração";
                grid.Rows.Add(names.GetValueOrDefault(id,"Nome não identificado"),id,s.Name,action);
            }
            current=plan;
            status.Text=plan.Ids.Length+" mods únicos ativos • origem "+plan.Source.Name+" → "+plan.Target.Name;
            Write("Origem: "+plan.Source.Path+" — "+plan.Source.Name);Write("Destino: "+plan.Target.Path+" — "+plan.Target.Name);Write("Instalação: "+plan.Install);
            Write("Simulação concluída. Nenhum arquivo alterado. Revise a lista e confirme instalação exclusiva.");
            if(names.Values.Any(n=>n=="Nome não identificado"))Write("Alguns nomes não puderam ser consultados. Confira os IDs na Workshop.");
        }catch(Exception e){Write("Análise bloqueada: "+e.Message);status.Text="Análise falhou.";}finally{Busy(false);}
    }
    static async Task<Dictionary<string,string>> Names(string[] ids,string install,bool online){
        var result=new Dictionary<string,string>();
        foreach(var id in ids){
            var p=Path.Combine(install,"mods","workshop-"+id,"modinfo.lua");
            try{if(File.Exists(p)&&new FileInfo(p).Length<1_000_000){Engine.SafePath(p);var m=Regex.Match(File.ReadAllText(p),"""(?m)^\s*name\s*=\s*["']([^"'\r\n]+)["']""");if(m.Success)result[id]=m.Groups[1].Value+" (local)";}}catch{}
        }
        if(online)try{
            using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(12)};
            foreach(var chunk in ids.Chunk(50)){
                var data=new Dictionary<string,string>{{"itemcount",chunk.Length.ToString()}};for(int i=0;i<chunk.Length;i++)data["publishedfileids["+i+"]"]=chunk[i];
                using var response=await http.PostAsync("https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/",new FormUrlEncodedContent(data));response.EnsureSuccessStatusCode();
                using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                foreach(var item in json.RootElement.GetProperty("response").GetProperty("publishedfiledetails").EnumerateArray()){
                    var id=item.GetProperty("publishedfileid").GetString();if(id==null||!chunk.Contains(id))continue;
                    if(item.TryGetProperty("result",out var r)&&r.GetInt32()==1&&item.TryGetProperty("title",out var title)&&(!item.TryGetProperty("consumer_app_id",out var app)||app.GetInt32()==322330))result[id]=(title.GetString()??"Nome não identificado").Replace("\r"," ").Replace("\n"," ");
                    else result[id]="Workshop indisponível / privada";
                }
            }
        }catch{}
        foreach(var id in ids)result.TryAdd(id,"Nome não identificado");return result;
    }
    async Task Reset(){
        var p=current;if(p==null||!exclusive.Checked)return;
        if(MessageBox.Show("Origem: "+p.Source.Name+" (Cluster_"+p.Source.Number+")\n"+p.Source.Path+"\n\n"+p.Ids.Length+" mods únicos. A lista na tela corresponde ao plano.\nConfirma esta origem?","Confirmação 1 de 2",MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;
        if(grid.Rows.Cast<DataGridViewRow>().Any(r=>Convert.ToString(r.Cells[0].Value) is ("Workshop indisponível / privada" or "Nome não identificado")&&Convert.ToString(r.Cells[3].Value)!="Remover da configuração")){Write("Mod indisponível ou sem nome identificado na origem: reset bloqueado. Ative a consulta Workshop e analise novamente.");return;}
        using var dialog=new Form{Text="Confirmação 2 de 2",Size=new Size(620,300),StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false};
        var layout=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(15)};
        var phrase="RESET Cluster_"+p.Target.Number;
        var label=new Label{Text="Destino: "+p.Target.Name+"\n"+p.Target.Path+"\n\nSomente caches V1 dos IDs afetados e UGC do destino serão movidos.\nMods removidos podem afetar entidades ao iniciar o mundo.\nDigite exatamente: "+phrase,AutoSize=true,MaximumSize=new Size(570,0)};
        var input=new TextBox{Width=450};var confirm=new Button{Text="Executar",Enabled=false,DialogResult=DialogResult.OK};var cancel=new Button{Text="Cancelar",DialogResult=DialogResult.Cancel};input.TextChanged+=(_,_)=>confirm.Enabled=input.Text==phrase;
        layout.Controls.Add(label);layout.Controls.Add(input);layout.Controls.Add(confirm);layout.Controls.Add(cancel);dialog.Controls.Add(layout);dialog.CancelButton=cancel;
        if(dialog.ShowDialog(this)!=DialogResult.OK||input.Text!=phrase)return;
        Busy(true);try{status.Text="Manutenção em andamento";var result=await Task.Run(()=>Engine.Apply(p,Write));Write("Atualizadores concluídos. Backup: "+result);Write("Confira os logs e teste a entrada no mundo e nas cavernas. Não reiniciamos o servidor.");status.Text="Manutenção concluída; validação do mundo pendente.";}catch(Exception e){Write(e.Message);status.Text="Falha: leia o relatório antes de iniciar o mundo.";}finally{current=null;Busy(false);}
    }
    static void Open(string url){try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}catch(Exception e){MessageBox.Show(e.Message);}}
    static void OpenBackups(){var p=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Gerenciador de Dedicado","Backups");Directory.CreateDirectory(p);Open(p);}
}
