using System;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Threading.Tasks;

sealed class CampaignModeDashboard:Form {
 readonly TrainerSession session;readonly CampaignEngine engine;readonly bool mock;
 readonly Label summary=Theme.Label("",10),detail=Theme.Label("",10);
 readonly TabControl tabs=new TabControl();readonly Dictionary<TabPage,ListBox> lists=new Dictionary<TabPage,ListBox>();
 Button refresh,open;public CampaignAction Choice;
 static readonly string[][] groups={
  new[]{Lang.T("Авантюрист: лагерь и контракты")},
  new[]{Lang.T("Кочевник: стада и стоянки"),Lang.T("Авантюрист: лагерь и контракты")},
  new[]{Lang.T("Административный правитель"),Lang.T("Государство и договоры")},
  new[]{Lang.T("Выбранная война и армия"),Lang.T("Титулы, земля и границы"),Lang.T("Государство и договоры")}
 };
 public CampaignModeDashboard(TrainerSession s,CampaignEngine e,bool mock=false){session=s;engine=e;this.mock=mock;Text=Lang.T("CK3 · Панели форм игры");ClientSize=new Size(920,670);MinimumSize=Size;StartPosition=FormStartPosition.CenterParent;BackColor=Theme.Bg;ForeColor=Theme.Text;Font=new Font("Segoe UI",10);
  summary.SetBounds(20,16,870,65);summary.ForeColor=Theme.Gold;Controls.Add(summary);
  tabs.SetBounds(20,88,875,420);tabs.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;tabs.Font=new Font("Segoe UI",10);Controls.Add(tabs);
  string[] names={Lang.T("Авантюрист"),Lang.T("Кочевник"),Lang.T("Административное"),Lang.T("Земельный правитель")};for(int i=0;i<names.Length;i++){int index=i;var page=new TabPage(names[i]);page.BackColor=Theme.Panel;var list=new ListBox{BackColor=Theme.Panel,ForeColor=Theme.Text,BorderStyle=BorderStyle.None,Font=new Font("Segoe UI",11),IntegralHeight=false,Dock=DockStyle.Fill};foreach(var a in engine.Actions.Where(a=>groups[index].Contains(a.group)))list.Items.Add(a);list.SelectedIndexChanged+=delegate{UpdateDetail();};list.DoubleClick+=delegate{Accept();};page.Controls.Add(list);lists.Add(page,list);tabs.TabPages.Add(page);}
  tabs.SelectedIndexChanged+=delegate{UpdateDetail();};detail.SetBounds(20,518,875,76);detail.Anchor=AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;detail.ForeColor=Theme.Muted;Controls.Add(detail);
  refresh=Theme.Button(Lang.T("Обновить состояние"),async delegate{await RefreshState();});refresh.SetBounds(20,610,280,40);refresh.Anchor=AnchorStyles.Bottom|AnchorStyles.Left;open=Theme.Button(Lang.T("Открыть выбранное действие"),delegate{Accept();},true);open.SetBounds(565,610,330,40);open.Anchor=AnchorStyles.Bottom|AnchorStyles.Right;Controls.Add(refresh);Controls.Add(open);Shown+=async delegate{if(!mock)await RefreshState();else ShowSnapshot();};
 }
 void ShowSnapshot(){var state=mock?null:session.State;string government=mock?Lang.T("Авантюрист"):state.Government;summary.Text=(mock?Lang.T("Демонстрация"):state.Name+" · "+state.Metrics)+Lang.T("\r\nПравление: ")+government+Lang.T(" · владений: ")+(mock?0:state.Provinces.Count);if(!mock){string g=government.ToLowerInvariant();int ix=g.Contains("администра")?2:g.Contains("кочевн") || g.Contains("nomad")?1:g.Contains("авантюр") || g.Contains("adventur") || g.Contains("landless")?0:3;tabs.SelectedIndex=ix;}UpdateDetail();}
 async Task RefreshState(){if(mock)return;refresh.Enabled=false;try{await session.ReadSnapshot(false);ShowSnapshot();}catch(Exception ex){detail.Text=Lang.T("Не удалось обновить: ")+ex.Message;}finally{refresh.Enabled=true;}}
 void UpdateDetail(){var page=tabs.SelectedTab;if(page==null)return;var list=lists[page];if(list.SelectedIndex<0 && list.Items.Count>0)list.SelectedIndex=0;var a=list.SelectedItem as CampaignAction;detail.Text=(a==null?Lang.T("Действий пока нет."):a.description)+Lang.T("\r\nДоступность конкретной функции проверяется перед выполнением; выбранное действие откроется в редакторе с предпросмотром.");open.Enabled=a!=null;}
 void Accept(){var page=tabs.SelectedTab;if(page==null)return;Choice=lists[page].SelectedItem as CampaignAction;if(Choice!=null){DialogResult=DialogResult.OK;Close();}}
}
