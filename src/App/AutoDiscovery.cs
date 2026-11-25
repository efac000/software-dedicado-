using System.Runtime.InteropServices;
using System.Diagnostics;
using Microsoft.Win32;
using Dedicado;
namespace Gerenciador;
sealed partial class MainForm {
    string? selectedSteamCmd;
    static string SystemSummary()=> $"Windows (build {Environment.OSVersion.Version.Build}) | SO {RuntimeInformation.OSArchitecture} | aplicativo {RuntimeInformation.ProcessArchitecture}";
    static DiscoveryResult DiscoverSystem(string[] manual){
        var steam=new List<string>();
        foreach(var key in new[]{@"HKEY_CURRENT_USER\Software\Valve\Steam",@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam",@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam"}){
            try{foreach(var value in new[]{"SteamPath","InstallPath"})if(Registry.GetValue(key,value,null) is string path)steam.Add(path);}catch{}
        }
        foreach(var folder in new[]{Environment.SpecialFolder.ProgramFiles,Environment.SpecialFolder.ProgramFilesX86}){
            var path=Environment.GetFolderPath(folder);if(!string.IsNullOrWhiteSpace(path))steam.Add(Path.Combine(path,"Steam"));
        }
        var profile=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var cmd=new List<string>{SteamBootstrap.ManagedDirectory};
        var server=new List<string>();
        var clusters=new List<string>();
        foreach(var key in new[]{
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 343050",
            @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 343050",
            @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 343050"}){
            try{if(Registry.GetValue(key,"InstallLocation",null) is string path&&Path.IsPathFullyQualified(path))server.Add(path);}catch{}
        }
        foreach(var name in new[]{"dontstarve_dedicated_server_nullrenderer_x64","dontstarve_dedicated_server_nullrenderer","steamcmd"}){
            foreach(var process in Process.GetProcessesByName(name)){
                using(process){
                    try{
                        var path=process.MainModule?.FileName;
                        if(path==null||!Path.IsPathFullyQualified(path))continue;
                        var folder=Path.GetDirectoryName(path);
                        if(folder==null)continue;
                        if(name=="steamcmd"){cmd.Add(folder);continue;}
                        if(Path.GetFileName(folder).Equals("bin64",StringComparison.OrdinalIgnoreCase)||Path.GetFileName(folder).Equals("bin",StringComparison.OrdinalIgnoreCase)){
                            var parent=Path.GetDirectoryName(folder);if(parent!=null)server.Add(parent);
                        }
                    }catch(System.ComponentModel.Win32Exception){}catch(InvalidOperationException){}catch(NotSupportedException){}
                }
            }
        }
        if(!string.IsNullOrWhiteSpace(profile)){
            cmd.Add(Path.Combine(profile,"steamcmd"));
            server.Add(Path.Combine(profile,"dst-server"));server.Add(Path.Combine(profile,"dedicado"));
        }
        foreach(var path in new[]{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),Path.Combine(profile,"Documents")}){
            if(Path.IsPathFullyQualified(path))clusters.Add(Path.Combine(path,"Klei","DoNotStarveTogether"));
        }
        foreach(var name in new[]{"OneDrive","OneDriveConsumer","OneDriveCommercial"}){
            if(Environment.GetEnvironmentVariable(name) is string path&&Path.IsPathFullyQualified(path))
                clusters.Add(Path.Combine(path,"Documents","Klei","DoNotStarveTogether"));
        }
        foreach(var name in new[]{"STEAMCMD_HOME","STEAMCMD_DIR"}){
            if(Environment.GetEnvironmentVariable(name) is string path&&Path.IsPathFullyQualified(path))cmd.Add(path);
        }
        foreach(var part in (Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator).Take(128)){
            var path=part.Trim().Trim('"');
            if(Path.IsPathFullyQualified(path)&&Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)).Equals("steamcmd",StringComparison.OrdinalIgnoreCase))cmd.Add(path);
        }
        foreach(var drive in DriveInfo.GetDrives()){
            try{
                if(drive.DriveType!=DriveType.Fixed||!drive.IsReady)continue;
                var path=drive.RootDirectory.FullName;
                steam.Add(Path.Combine(path,"Steam"));steam.Add(Path.Combine(path,"SteamLibrary"));
                cmd.Add(Path.Combine(path,"steamcmd"));
                foreach(var folder in new[]{"DST","DSTServer","DST-Server","Dedicado","Servers","Jogos","Games","Don't Starve Together Dedicated Server"})server.Add(Path.Combine(path,folder));
                clusters.Add(Path.Combine(path,"Klei","DoNotStarveTogether"));
            }catch(IOException){}catch(UnauthorizedAccessException){}
        }
        server.AddRange(manual);clusters.AddRange(manual);
        return Discovery.Scan(steam,server,clusters,cmd);
    }
    async Task AutoLocate(bool startup=false){
        if(busy)return;
        var paths=new[]{install.Text,destinationRoot.Text,root.Text};
        Busy(true);
        DiscoveryResult found;
        try{
            status.Text="Localizando instalações e mundos neste computador...";
            found=await Task.Run(()=>DiscoverSystem(paths));
        }catch(Exception e){Write("Detecção incompleta: "+e.Message);return;}
        finally{Busy(false);}
        Write(SystemSummary());
        Write($"Detecção local: {found.Installs.Length} instalação(ões), {found.Clusters.Length} mundo(s), {found.SteamCmd.Length} SteamCMD.");
        if(startup){
            if(found.SteamCmd.Length==1)selectedSteamCmd=found.SteamCmd[0];
            if(string.IsNullOrWhiteSpace(install.Text)&&found.Installs.Length==1)install.Text=found.Installs[0].Path;
            if(target.Items.Count==0&&found.Clusters.Length==1){destinationRoot.Text=found.Clusters[0].Path;RefreshClusters();}
            if(found.Clusters.Length>1)target.SelectedIndex=-1;
            if(found.Installs.Length>1||found.Clusters.Length>1||found.SteamCmd.Length>1)status.Text="Vários destinos encontrados. Use Detectar dedicado para escolher.";
            return;
        }
        using var dialog=new Form{Text="Detecção local do dedicado",Size=new Size(940,480),MinimumSize=new Size(700,420),StartPosition=FormStartPosition.CenterParent,BackColor=Bg,ForeColor=Ink,Font=Font,Icon=Icon};
        var info=new Label{Dock=DockStyle.Top,Height=65,ForeColor=Accent,Text=SystemSummary()+"\nSelecione os destinos explicitamente. Nenhum arquivo será alterado."};
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=6,Padding=new Padding(10)};
        var installs=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,BackColor=PanelColor,ForeColor=Ink};
        var worlds=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,BackColor=PanelColor,ForeColor=Ink};
        var cli=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,BackColor=PanelColor,ForeColor=Ink};
        installs.Items.AddRange(found.Installs.Cast<object>().ToArray());worlds.Items.AddRange(found.Clusters.Cast<object>().ToArray());cli.Items.AddRange(found.SteamCmd.Cast<object>().ToArray());
        if(installs.Items.Count==1)installs.SelectedIndex=0;
        if(worlds.Items.Count==1)worlds.SelectedIndex=0;
        if(cli.Items.Count==1)cli.SelectedIndex=0;
        if(selectedSteamCmd!=null&&cli.Items.Contains(selectedSteamCmd))cli.SelectedItem=selectedSteamCmd;
        foreach(var entry in new (string,Control)[]{("Instalação dedicada ("+installs.Items.Count+")",installs),("Cluster / mundo ("+worlds.Items.Count+")",worlds),("SteamCMD encontrado ("+cli.Items.Count+")",cli)}){
            entry.Item2.AccessibleName=entry.Item1;layout.RowStyles.Add(new RowStyle(SizeType.Absolute,30));layout.Controls.Add(new Label{Text=entry.Item1,AutoSize=true});
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,40));layout.Controls.Add(entry.Item2);
        }
        var footer=new Label{Dock=DockStyle.Bottom,Height=45,ForeColor=Muted,Text="A detecção não pesquisa a unidade inteira, não executa arquivos e não lê tokens. Use Procurar para caminhos personalizados."};
        var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=46,FlowDirection=FlowDirection.RightToLeft};
        var apply=new Button{Text="Usar seleção",DialogResult=DialogResult.OK};var cancel=new Button{Text="Cancelar",DialogResult=DialogResult.Cancel};Style(apply);Style(cancel);
        apply.Enabled=installs.SelectedItem!=null||worlds.SelectedItem!=null||cli.SelectedItem!=null;
        void Update()=>apply.Enabled=installs.SelectedItem!=null||worlds.SelectedItem!=null||cli.SelectedItem!=null;
        installs.SelectedIndexChanged+=(_,_)=>Update();worlds.SelectedIndexChanged+=(_,_)=>Update();cli.SelectedIndexChanged+=(_,_)=>Update();
        actions.Controls.AddRange(new Control[]{cancel,apply});dialog.Controls.Add(layout);dialog.Controls.Add(info);dialog.Controls.Add(footer);dialog.Controls.Add(actions);dialog.CancelButton=cancel;
        PrepareAccessibility(dialog);if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        exclusive.Checked=false;
        if(cli.SelectedItem is string selectedCli)selectedSteamCmd=selectedCli;
        if(installs.SelectedItem is DetectedInstall selected)install.Text=selected.Path;
        if(worlds.SelectedItem is Cluster cluster)destinationRoot.Text=cluster.Path;
        RefreshClusters();
        status.Text="Seleção de detecção aplicada. Analise e confirme antes de executar.";
    }
}