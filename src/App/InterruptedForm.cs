using Dedicado;
namespace Gerenciador;
sealed partial class MainForm{
    async Task RecoverInterrupted(string rootPath){
        if(target.SelectedItem is not Cluster chosen){Write("Há manutenção interrompida. Selecione o cluster original e abra a Central de backups novamente.");return;}
        var pending=InterruptedMaintenance.Pending(rootPath);
        using var dialog=new ModernDialog{Text="Manutenção interrompida — recuperar",Size=new Size(800,480),MinimumSize=new Size(620,400),StartPosition=FormStartPosition.CenterParent,BackColor=Bg,ForeColor=Ink,Font=Font};
        var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(12)};
        var hint=new Label{AutoSize=true,MaximumSize=new Size(690,0),Text="Uma operação anterior não foi finalizada. Novas manutenções ficam bloqueadas. Selecione o backup original. A recuperação restaura apenas arquivos e caches afetados, não os saves. Desligue servidor, jogo e reinícios automáticos. Arquivos modificados fora do plano bloqueiam a operação.\nDestino selecionado: "+chosen.Path};
        var list=new ListBox{Width=690,Height=130,BackColor=PanelColor,ForeColor=Ink,HorizontalScrollbar=true};list.Items.AddRange(pending.Cast<object>().ToArray());
        var phrase="RECUPERAR "+chosen.FolderName;
        var label=new Label{AutoSize=true,Text="Digite exatamente: "+phrase};
        var input=new TextBox{Width=380,BackColor=PanelColor,ForeColor=Ink};
        var exclusiveInstall=new CheckBox{AutoSize=true,Text="Confirmo instalação exclusiva e servidor/reinício automático desligados."};
        var recover=new Button{Text="Recuperar operação",Enabled=false,DialogResult=DialogResult.OK};var close=new Button{Text="Cancelar",DialogResult=DialogResult.Cancel};Style(recover);Style(close);
        void Refresh()=>recover.Enabled=list.SelectedItem is string&&input.Text==phrase&&exclusiveInstall.Checked;
        list.SelectedIndexChanged+=(_,_)=>Refresh();input.TextChanged+=(_,_)=>Refresh();exclusiveInstall.CheckedChanged+=(_,_)=>Refresh();
        panel.Controls.AddRange(new Control[]{hint,list,exclusiveInstall,label,input,recover,close});dialog.Controls.Add(panel);dialog.CancelButton=close;
        if(dialog.ShowDialog(this)!=DialogResult.OK||list.SelectedItem is not string backup||input.Text!=phrase||!exclusiveInstall.Checked)return;
        var installation=install.Text;
        if(Notice.Show(this,"Recuperar a configuração anterior e os caches afetados em "+chosen.Path+"?","Confirmação final",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;
        InvalidatePlan();exclusive.Checked=false;Busy(true);
        try{status.Text="Recuperando operação interrompida...";await Task.Run(()=>InterruptedMaintenance.Recover(backup,chosen,installation,Write));status.Text="Recuperação concluída. Teste o mundo antes de iniciar produção.";}
        catch(Exception ex){Write(ex.Message);status.Text="Recuperação não confirmada. Mantenha servidor desligado.";}
        finally{Busy(false);}
    }
}
