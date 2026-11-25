using Dedicado;
namespace Gerenciador;
sealed partial class MainForm{
    WorkshopHit? BrowseWorkshop(IWin32Window owner,string initial,string shard){
        using var dialog=new ModernDialog{Text="Pesquisa global Workshop - "+shard,Size=new Size(850,540),MinimumSize=new Size(620,420),StartPosition=FormStartPosition.CenterParent,BackColor=Bg,ForeColor=Ink,Font=Font,Icon=Icon};
        var bar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=85,AutoScroll=true};
        var key=new TextBox{Width=210,UseSystemPasswordChar=true,PlaceholderText="Chave Steam Web API (usuário)",BackColor=PanelColor,ForeColor=Ink,MaxLength=32};
        var query=new TextBox{Width=240,Text=initial,PlaceholderText="Nome do mod",BackColor=PanelColor,ForeColor=Ink,MaxLength=120};
        var search=new ModernButton{Text="Pesquisar"};var next=new ModernButton{Text="Próxima página",Enabled=false};var browser=new ModernButton{Text="Abrir busca oficial"};
        var hint=new Label{Dock=DockStyle.Bottom,Height=54,ForeColor=Muted,Text="A chave de usuário é usada apenas nesta sessão, no cabeçalho HTTPS da Steam. Não use uma chave de distribuidora."};
        var list=new ListBox{Dock=DockStyle.Fill,BackColor=PanelColor,ForeColor=Ink,DisplayMember="Title",HorizontalScrollbar=true};
        var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=48,FlowDirection=FlowDirection.RightToLeft};
        var add=new ModernButton{Text="Adicionar ao "+shard,Enabled=false};var cancel=new ModernButton{Text="Cancelar",DialogResult=DialogResult.Cancel};
        foreach(var b in new[]{search,next,browser,add,cancel})Style(b);
        bar.Controls.AddRange(new Control[]{query,key,search,next,browser});
        actions.Controls.AddRange(new Control[]{cancel,add});
        dialog.Controls.Add(list);dialog.Controls.Add(bar);dialog.Controls.Add(hint);dialog.Controls.Add(actions);dialog.CancelButton=cancel;
        using var lifetime=new CancellationTokenSource();
        string? cursor=null,submitted=null;WorkshopHit? selected=null;bool loading=false;
        async Task Load(bool advance){
            if(loading)return;
            loading=true;search.Enabled=next.Enabled=add.Enabled=false;key.Enabled=query.Enabled=false;list.Items.Clear();
            var term=advance?submitted??query.Text:query.Text;
            try{
                if(string.IsNullOrWhiteSpace(key.Text)){
                    hint.Text="A pesquisa global exige uma chave de usuário Steam Web API. Sem chave, use Abrir busca oficial e importe o ID ou link no catálogo.";
                    cursor=null;return;
                }
                var page=await WorkshopSearch.Search(key.Text,term,advance?cursor:null,lifetime.Token);
                if(dialog.IsDisposed||lifetime.IsCancellationRequested)return;
                list.Items.AddRange(page.Items.Cast<object>().ToArray());cursor=page.NextCursor;submitted=term;
                hint.Text=page.Items.Length==0?(page.ReceivedCount>0?"A Steam retornou itens sem metadados DST válidos nesta página.":"Nenhum resultado encontrado nesta página."):page.Items.Length+" resultados nesta página. Escolha o mod para "+shard+".";
            }catch(OperationCanceledException){}catch(Exception ex){if(!dialog.IsDisposed&&!lifetime.IsCancellationRequested)hint.Text=ex is IOException or ArgumentException?ex.Message:"Falha na consulta Workshop.";cursor=null;}
            finally{loading=false;if(!dialog.IsDisposed&&!lifetime.IsCancellationRequested){search.Enabled=key.Enabled=query.Enabled=true;next.Enabled=cursor!=null;add.Enabled=list.SelectedItem is WorkshopHit;}}
        }
        search.Click+=async(_,_)=>await Load(false);next.Click+=async(_,_)=>await Load(true);
        query.AccessibleName="Nome do mod na Workshop";key.AccessibleName="Chave de usuário Steam Web API (não armazenada)";
        list.AccessibleName="Resultados da Workshop para "+shard;
        PrepareAccessibility(dialog);
        dialog.AcceptButton=search;
        browser.Click+=(_,_)=>Open(WorkshopId.SearchUri(query.Text));
        query.TextChanged+=(_,_)=>{cursor=null;next.Enabled=false;list.Items.Clear();};
        key.TextChanged+=(_,_)=>{cursor=null;next.Enabled=false;list.Items.Clear();};
        list.SelectedIndexChanged+=(_,_)=>add.Enabled=!loading&&list.SelectedItem is WorkshopHit;
        add.Click+=(_,_)=>{if(!loading&&list.SelectedItem is WorkshopHit hit){selected=hit;dialog.DialogResult=DialogResult.OK;}};
        dialog.FormClosing+=(_,_)=>lifetime.Cancel();
        var result=dialog.ShowDialog(owner);key.Clear();
        return result==DialogResult.OK?selected:null;
    }
}