using Dedicado;

namespace Gerenciador;

sealed partial class MainForm {
    string EditModOptions(IWin32Window owner,string text,string id,string shard,DeclaredModOption[] declared){
        var initial=text;
        if(declared.Length>0)text=Lua.ImportDeclaredOptions(text,id,declared);
        var options=Lua.GetOptions(text,id);
        if(options.Count==0){
            Notice.Show(owner,"Este mod ainda não tem opções simples gravadas neste shard.","Opções do mod",MessageBoxButtons.OK,MessageBoxIcon.Information);
            return text;
        }

        using var dialog=new ModernDialog{
            Text=$"Opções do mod {id} — {shard}",Size=new Size(640,530),MinimumSize=new Size(480,360),
            StartPosition=FormStartPosition.CenterParent,BackColor=Bg,ForeColor=Ink,Font=Font,ShowInTaskbar=false,Icon=Icon
        };
        var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true};
        var list=new TableLayoutPanel{
            Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,
            ColumnCount=2,Padding=new Padding(14)
        };
        scroll.Controls.Add(list);
        list.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,45));
        list.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,55));
        var fields=new Dictionary<string,Func<string>>(StringComparer.Ordinal);
        foreach(var option in options){
            list.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var declaration=declared.FirstOrDefault(x=>x.Name==option.Name);
            list.Controls.Add(new Label{Text=declaration?.Label??option.Name,AutoSize=true,Anchor=AnchorStyles.Left});
            if(declaration!=null&&declaration.Default.Kind==option.Kind){
                var choices=declaration.Choices.ToList();
                if(!choices.Any(x=>x.Value==option.Value))choices.Insert(0,new ModChoice("Valor atual (fora das escolhas): "+option.Value,option.Kind,option.Value));
                var combo=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,BackColor=PanelColor,ForeColor=Ink,Anchor=AnchorStyles.Left|AnchorStyles.Right,DisplayMember="Label",AccessibleName=declaration.Label,AccessibleDescription=option.Name};
                combo.Items.AddRange(choices.Cast<object>().ToArray());
                combo.SelectedIndex=choices.FindIndex(x=>x.Value==option.Value);
                list.Controls.Add(combo);
                fields.Add(option.Name,()=>((ModChoice)combo.SelectedItem!).Value);
                continue;
            }
            if(option.Kind==LuaOptionKind.Boolean){
                var check=new CheckBox{
                    Text="Ativado",Checked=option.Value=="true",AutoSize=true,
                    AccessibleName=option.Name,Anchor=AnchorStyles.Left
                };
                list.Controls.Add(check);
                fields.Add(option.Name,()=>check.Checked?"true":"false");
            }else{
                var input=new TextBox{
                    Text=option.Value,BackColor=PanelColor,ForeColor=Ink,BorderStyle=BorderStyle.FixedSingle,
                    AccessibleName=option.Name,Anchor=AnchorStyles.Left|AnchorStyles.Right
                };
                list.Controls.Add(input);
                fields.Add(option.Name,()=>input.Text);
            }
        }
        var header=new Label{
            Dock=DockStyle.Top,Padding=new Padding(14,0,14,0),Height=52,TextAlign=ContentAlignment.MiddleLeft,
            Text="Edite as opções gravadas. O plano ainda precisa ser revisado.",ForeColor=Muted
        };
        var save=new ModernButton{Text="Salvar no plano"};
        var cancel=new ModernButton{Text="Cancelar",DialogResult=DialogResult.Cancel};
        Style(save);
        Style(cancel);
        var actions=new FlowLayoutPanel{
            Dock=DockStyle.Bottom,Height=48,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(8,0,8,0)
        };
        actions.Controls.AddRange(new Control[]{cancel,save});
        dialog.Controls.Add(scroll);
        dialog.Controls.Add(header);
        dialog.Controls.Add(actions);
        dialog.CancelButton=cancel;
        dialog.AcceptButton=save;

        var edited=text;
        save.Click+=(_,_)=>{
            try{
                edited=Lua.SetOptions(text,id,fields.ToDictionary(field=>field.Key,field=>field.Value(),StringComparer.Ordinal));
                dialog.DialogResult=DialogResult.OK;
            }catch(FormatException error){
                Notice.Show(dialog,error.Message,"Opção inválida",MessageBoxButtons.OK,MessageBoxIcon.Warning);
            }
        };
        PrepareAccessibility(dialog);return dialog.ShowDialog(owner)==DialogResult.OK?edited:initial;
    }
}
