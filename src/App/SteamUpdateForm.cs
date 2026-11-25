using Dedicado;
namespace Gerenciador;
sealed partial class MainForm {
    async Task UpdateDedicated(){
        if(busy)return;
        if(target.SelectedItem is not Cluster chosen){Write("Selecione o cluster do dedicado antes de atualizar.");return;}
        var installation=install.Text;
        var existing=selectedSteamCmd;
        if(existing!=null){try{Engine.SafePath(existing);if(!File.Exists(existing))existing=null;}catch{existing=null;}}
        existing??=SteamBootstrap.Find();
        string exe;
        var choice=MessageBox.Show(this,existing!=null?"SteamCMD localizado:\n"+existing+"\n\nSim: usar este executável. Não: selecionar outro.":"SteamCMD não localizado.\n\nSim: baixar a versão oficial da Valve para a pasta local do aplicativo. Não: selecionar um steamcmd.exe existente.","SteamCMD",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question,MessageBoxDefaultButton.Button3);
        if(choice==DialogResult.Cancel)return;
        if(choice==DialogResult.Yes&&existing!=null)exe=existing;
        else if(choice==DialogResult.Yes){
            if(MessageBox.Show(this,"Baixar SteamCMD de:\n"+SteamBootstrap.OfficialUrl+"\n\nDestino: "+SteamBootstrap.ManagedDirectory+"\nO download não iniciará o executável. O pacote será conferido por estrutura e HTTPS; não há hash oficial fixo incorporado.","Instalar SteamCMD",MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;
            Busy(true);
            try{status.Text="Baixando SteamCMD oficial...";exe=await SteamBootstrap.InstallAsync(Write);status.Text="SteamCMD instalado. Revise a atualização do dedicado.";}
            catch(Exception e){Write("Instalação SteamCMD bloqueada: "+e.Message);status.Text="SteamCMD não instalado.";return;}
            finally{Busy(false);}
        }else{
            using var picker=new OpenFileDialog{Title="Selecione o SteamCMD oficial (steamcmd.exe)",Filter="SteamCMD (steamcmd.exe)|steamcmd.exe",CheckFileExists=true};
            if(existing!=null){picker.InitialDirectory=Path.GetDirectoryName(existing)!;picker.FileName=existing;}
            if(picker.ShowDialog(this)!=DialogResult.OK)return;
            exe=picker.FileName;
        }
        try{SteamUpdate.Check(exe,chosen,installation);Engine.Offline();}
        catch(Exception e){Write("Atualização bloqueada: "+e.Message);return;}
        using var dialog=new Form{Text="Atualização do dedicado via SteamCMD",Size=new Size(720,450),MinimumSize=new Size(620,400),StartPosition=FormStartPosition.CenterParent,BackColor=Bg,ForeColor=Ink,Font=Font};
        var body=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(12)};
        bool resuming;
        try{resuming=SteamUpdate.Pending(Recovery.DefaultRoot).Length>0;}
        catch(IOException error){Write(error.Message);return;}
        var phrase=(resuming?"RETOMAR ":"ATUALIZAR ")+chosen.FolderName;
        var info=new Label{AutoSize=true,MaximumSize=new Size(640,0),Text="Destino: "+installation+"\nCluster: "+chosen.Path+"\nSteamCMD: "+exe+"\n\nA operação atualiza o servidor (app 343050), não os mods. Faz backup do mundo e dos arquivos de configuração dos mods. Não reverte binários após falha. Reserve espaço para vários GiB e feche o servidor e reinícios automáticos. Instalações gerenciadas pela Steam não são suportadas neste modo."};
        if(resuming)info.Text="Atualização anterior pendente. A retomada valida os binários e exige que a configuração atual corresponda ao backup.\n\n"+info.Text;
        var exclusiveInstall=new CheckBox{AutoSize=true,Text="Confirmo instalação exclusiva, UGC padrão e ausência de reinício automático."};
        var validate=new CheckBox{AutoSize=true,Checked=resuming,Enabled=!resuming,Text="Também validar arquivos do dedicado (pode demorar e substituir arquivos)."};
        var label=new Label{AutoSize=true,Text="Digite: "+phrase};
        var input=new TextBox{Width=410,BackColor=PanelColor,ForeColor=Ink};
        var update=new Button{Text="Confirmar atualização",Enabled=false,DialogResult=DialogResult.OK};var cancel=new Button{Text="Cancelar",DialogResult=DialogResult.Cancel};Style(update);Style(cancel);
        void Refresh()=>update.Enabled=exclusiveInstall.Checked&&input.Text==phrase;
        exclusiveInstall.CheckedChanged+=(_,_)=>Refresh();input.TextChanged+=(_,_)=>Refresh();
        body.Controls.Add(info);body.Controls.Add(exclusiveInstall);body.Controls.Add(validate);body.Controls.Add(label);body.Controls.Add(input);body.Controls.Add(update);body.Controls.Add(cancel);dialog.Controls.Add(body);dialog.CancelButton=cancel;
        if(dialog.ShowDialog(this)!=DialogResult.OK||!exclusiveInstall.Checked||input.Text!=phrase)return;
        if(MessageBox.Show(this,"Confirma a atualização do binário em "+installation+"? O servidor permanecerá desligado.","Confirmação final",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;
        var check=validate.Checked;InvalidatePlan();exclusive.Checked=false;Busy(true);
        try{
            status.Text="Atualizando o dedicado via SteamCMD...";
            var backup=await Task.Run(()=>SteamUpdate.Apply(exe,chosen,installation,check,Write));
            Write("SteamCMD concluiu. Backup e logs: "+backup);status.Text="Atualização concluída; teste o dedicado e analise os mods.";
        }catch(Exception e){Write(e.Message);status.Text="Atualização não confirmada. Não inicie antes de revisar os logs.";}
        finally{Busy(false);}
    }
}
