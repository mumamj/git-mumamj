using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LitJson;
using System;
using System.Text;
using BestHTTP.Extensions;
using System.Threading;
using CHANGETYPE = DBObject.CHANGETYPE;

public class ChurukuFilterChoiceGroup : MonoBehaviour
{
    #region Public Fields
    public DBScrollpanel mainDBScrollpanel;
    public DBContentController contentController;
    public DBRowList mainRowList;
    //TODO: shaixuan
    public RowlistFilter rowlistFilter;
    public DataSourceUserFilter riqiFilter;
    public DataSourceUserFilter cangkuFilter;
    public dfLabel yewuyuanLabel;
    public dfLabel labelchangku;
    public dfLabel zhuyi;
    public DBRowList huifuRow;
    public dfPanel message;
    public dfButton cangkuBtn;
    public dfTextbox danjubianhao;
    public lyTextbox RowCount;
    public lyTextbox description;
    public DataSourceUserFilter optionUserFilter;
    public DBButtonSingle optionButtonSingle;
    public DBButtonSingle obsingleUpMode;
    public List<lyTextbox> Hooklist;//对勾√
    public lyTextbox upSaleText;
    public lyTextbox unupSaleText;
    public List<dfCheckbox> listCheckbox;
    public Page curpage;
    #endregion

    #region Private Fields
    private DBRowList.Linq dictLinq = null;
    private DBRowList.Linq mainRowList_Linq = null;
    private string timeColumName = "issuetime";
    private static GameObject _obj;
    private static dfControl _control;
    private bool reset = false;
    #endregion

    // Use this for initialization
    void Awake()
    {
        dictLinq = (JsonData data, DBRowList rowlist) =>
        {
            var WarehouseTruck =
                from JsonData row in data
                where (string)row["usertype"] == "warehouse" || (string)row["usertype"] == "truck"
                select row;
            var People =
                from JsonData row in data
                where (string)row["usertype"] == "people" && (string)row["roleid"] == "10546cababcdefabcdefabcdefabcdef"
                group row by (string)row["userid"]
                    into groupRow
                    where WarehouseTruck.All(_row => (string)_row["usertype"] != "truck" || (string)_row["userid"] != groupRow.Key)
                    select groupRow.First();
            return WarehouseTruck.Union(People);
        };

        mainRowList.LinqObject = (JsonData data, DBRowList rowlist) =>
        {
            if (optionButtonSingle.Value == "tblbillswapin")
            {
                return from JsonData item in data
                       where (string)item["flowrootid_class"] == optionButtonSingle.Value
                       orderby (string)item["billexpecteddelivertime"] descending
                       group item by (string)item["newsguid"] into g
                       select HandleItemsCount(g);
            }
            else
            {
                return from JsonData item in data
                       where (string)item["flowrootid_class"] == optionButtonSingle.Value && obsingleUpMode.Value == "1"
                       || (string)item["flowrootid_class"] != optionButtonSingle.Value && obsingleUpMode.Value == "0"
                       orderby (string)item["billexpecteddelivertime"] descending
                       group item by (string)item["newsguid"] into g
                       select HandleItemsCount(g);
            }
        };
        rowlistFilter.AddOnLinq += RunLinq;
        mainRowList.formatCustomizeFunc = (DBRowList rowlist) =>
        {
            rowlist.InitColumnFormat("itemscount", "integer", "品相数量");
            rowlist.InitColumnFormat("issuedate", "date", "录单日期");
            rowlist.InitColumnFormat("scrapped", "integer", "是否恢复");
            rowlist.InitColumnFormat("flowclass", "text", "是否有源头");
        };
        zhuyi.Text = "注意：1、合并的单据已被拆分，您可以恢复后重新合并！    " + "2、按照最新销售单据明细找回并恢复单据！";
        optionButtonSingle.onValueChanged += option_ValueChanged;
        obsingleUpMode.onValueChanged += obsingle_ValueChanged;
        mainRowList.DataRefreshed += RefreshFromDB;
        mainDBScrollpanel.ExpandRowCreatedEvent += OnExpandRowCreated;
      
       
    }
    void OnEnable()
    {
        StartCoroutine(leixingchushi());
    }
    //初始化选择的单据类型
    public IEnumerator leixingchushi()
    {
        curpage.receivedParameters.Clear();
        while (curpage.receivedParameters.Count == 0)
        { yield return 0; }

        if (curpage == null) yield break;

        if (curpage.receivedParameters.ContainsKey("type"))
            optionButtonSingle.Value = (string)curpage.receivedParameters["type"];
        option_ValueChanged(optionButtonSingle.Value);
    }
    public IEnumerable<JsonData> RunLinq(IEnumerable<JsonData> data, DBRowList rowlist, IEnumerable<JsonData> originalData)
    {
        if (optionButtonSingle.Value == "tblbillswapin")
        {
            return from JsonData item in data
                   where (string)item["flowrootid_class"] == optionButtonSingle.Value
                   orderby (string)item["billexpecteddelivertime"] descending
                   group item by (string)item["newsguid"] into g
                   select HandleItemsCount(g);
        }
        else
        {
            return from JsonData item in data
                   where (string)item["flowrootid_class"] == optionButtonSingle.Value && obsingleUpMode.Value == "1"
                   || (string)item["flowrootid_class"] != optionButtonSingle.Value && obsingleUpMode.Value == "0"
                   orderby (string)item["billexpecteddelivertime"] descending
                   group item by (string)item["newsguid"] into g
                   select HandleItemsCount(g);
        }
    }

        #region BeforeSearchEvent EndSearchEvent, DataRefreshed of mainRowlist and subRowlist,RowRenderedEvent of RowTemplate
        void BeforeFetchingData(string value)
    {
        contentController.dbHeaderCheckBox.IsChecked = false;
        riqiFilter.clearValue();
        cangkuFilter.clearValue();
        danjubianhao.Value = "";
        rowlistFilter.Reset();
       // rowlistFilter.AddOnLinq = null;
        mainRowList.DataSource.storeProcedureParameters[0] = "\"" + value + "\"";
        mainRowList.DataSource.fetchData();
    }
    //控制备注信息的展示与是否可编辑   
    public void OnRowRenderedEvent(RowUIProperty _rowUiproperty)
    {
        if ((_rowUiproperty as DBRowTemplateUIProperty) == false) return;
        dfControl _description = ((_rowUiproperty as DBRowTemplateUIProperty)["maindescription"] as remarkButtonProperty).control;
        if (string.IsNullOrEmpty((string)_rowUiproperty.row.Value["description"]))
        {
            _description.IsVisible = false;
        }
        else
        {
            _description.IsVisible = true;
            _description.GetComponent<remarkButtonProperty>().Editable = false;
        }
    }

    private JsonData HandleItemsCount(IGrouping<string, JsonData> group)
    {
        JsonData newrow = new JsonData(JsonType.Object);
        newrow["flowrootid_class"] = group.First()["flowrootid_class"];
        newrow["serialnumber"] = group.First()["serialnumber"];
        newrow["productiondate"] = group.First()["productiondate"];
        newrow["parentid_class"] = group.First()["parentid_class"];
        newrow["issueby"] = group.First()["issueby"];
        newrow["billid"] = group.First()["billid"];
        newrow["shippingfrom_class"] = group.First()["shippingfrom_class"];
        newrow["shippingto"] = group.First()["shippingto"];
        newrow["issuetime"] = group.First()["issuetime"];
        newrow["promotionno"] = group.First()["promotionno"];
        newrow["description"] = group.First()["description"];
        newrow["billid_class"] = group.First()["billid_class"];
        newrow["flowrootid"] = group.First()["flowrootid"];
        newrow["parentid"] = group.First()["parentid"];
        newrow["instanceclassloc"] = group.First()["instanceclassloc"];
        newrow["shippingfrom"] = group.First()["shippingfrom"];
        newrow["shippingto_class"] = group.First()["shippingto_class"];
        newrow["instanceclass"] = group.First()["instanceclass"];
		newrow["newsguid"] = group.First()["newsguid"];
        newrow["batchcode"] = group.First()["batchcode"];
        newrow["billexpecteddelivertime"] = group.First()["billexpecteddelivertime"];
		newrow["sumitem"] =group.First()["sumitem"];
        newrow["sumtoexpire"] = group.First()["sumtoexpire"];
        newrow["sumquality"] = group.First()["sumquality"];
        newrow["sumexpire"] = group.First()["sumexpire"];
        newrow["sumdamage"] = group.First()["sumdamage"];
        newrow["instanceclassloc"] = group.First()["instanceclassloc"];
        newrow["itemscount"] = int.Parse(ItemsManager.GetItemsCount(group.Select(x => (string)x["itemobjid"])));
        newrow["issuedate"] = ((DateTime)group.First()["issuetime"]).ToString("yyyy-MM-dd");
        newrow["scrapped"] = 1;
        newrow["flowclass"] = optionButtonSingle.Value == "tblbillswapin"?"-1":obsingleUpMode.Value;
        return newrow;
    }

    //registered event of  mainRowList datarefresh
    void RefreshFromDB(DBObject dbo, DBObject.CHANGETYPE ctype, DBRow row, DBColumn column)
    {
        if (ctype == CHANGETYPE.DataSourceFetching)
        {
            RowCount.Text = "0条";
        }

        if (ctype == CHANGETYPE.DataSourceRefresh || ctype == CHANGETYPE.Delete || ctype == DBObject.CHANGETYPE.Add)
        {
            RowCount.Text = dbo.Value.Count.ToString() + "条";
        }

        if (ctype == DBObject.CHANGETYPE.Add)
        {

        }
        else if (ctype == CHANGETYPE.Update && row != null)
        {

        }
    }
    #endregion


    public void refreshDate(dfControl control, dfMouseEventArgs args)
    {
        BeforeFetchingData(optionButtonSingle.Value);
    }

    private DBRowList subRowList;
    void OnExpandRowCreated(DBRowTemplateUIProperty rowUIProperty)
    {
        rowUIProperty.control.Height = rowUIProperty.Parent.control.Height;
        rowUIProperty.ExpandDetailsHandler();

        if (_obj == null)
        {
            _obj = initDetailsListTabView();
        }
        _control = rowUIProperty.control.AddPrefab(_obj);

        StartCoroutine(InitTabs(rowUIProperty));
    }
    /// <summary>
    /// Inits the details list tab view.
    /// </summary>
    /// <returns>The details list tab view.</returns>
    GameObject initDetailsListTabView()
    {
        GameObject ret = null;
        ret = AssetsManager.getObject("prefabs/churukuhuifuTabView") as GameObject;
        if (ret == null)
            throw new UnityException("Load churukuhuifuTabView fail!");
        return ret;
    }
    private IEnumerator InitTabs(DBRowTemplateUIProperty rowUIProperty)
    {
        yield return 0;

        churukuhuifuTabView ltv = _control.gameObject.GetComponent<churukuhuifuTabView>();
        rowUIProperty.control.AddControl(_control);
        yield return 0;
        ltv.parentRow = rowUIProperty.row;
        ltv.Parent = rowUIProperty;

        _control.RelativePosition = new Vector3(10f, 75f, 0f);
        yield break;

    }
    private void option_ValueChanged(string value)
    {
        string changestr = value == "tblbillrestock" || value == "tblbillreturnout" ? "供应商" : value == "tblbillswapout" ? "收货仓库" :
        value == "tblbillswapin" ? "发货仓库" : "供应商";
        string changestrcangku = value == "tblbillrestock" || value == "tblbillreturnout" ? "仓库" : "发货仓库";
        string changestryewuyuan = value == "tblbillswapin" || value == "tblbillswapout" ? "收货仓库" : "供应商";
        cangkuBtn.Text = "仓库/" + changestr;
        yewuyuanLabel.Text = changestryewuyuan;
        labelchangku.Text = changestrcangku;
        if (value == "tblbillswapin")
        {
            foreach (dfCheckbox item in listCheckbox)
            {
                item.IsVisible = false;
            }
        }
        else
        {
            foreach (dfCheckbox item in listCheckbox)
            {
                item.IsVisible = true;
            }
        }
        upSaleText.Text = value == "tblbillrestock" ? "采购订单生成" : value == "tblbillreturnout" ? "采购退货生成" : value == "tblbillswapout" ? "销售单生成" : "";
        unupSaleText.Text = value == "tblbillrestock" ? "非采购订单生成" : value == "tblbillreturnout" ? "非采购退货生成" : value == "tblbillswapout" ? "非销售单生成" : "";

        var tempstr = value == "tblbillrestock" ? "采购订单" : value == "tblbillreturnout" ? "采购退货" : value == "tblbillswapout" ? "销售单据" : "";
        string tempval = obsingleUpMode.Value == "0" ? "2、按照最新" + tempstr + "明细找回并恢复单据！" : "2、此处仅保留7天内删除单据！";
        zhuyi.Text = value == "tblbillswapin" ? "此处仅保留7天内删除单据！" : "注意：1、合并的单据已被拆分，您可以恢复后重新合并！    " + tempval;
        BeforeFetchingData(value);
    }

    private void obsingle_ValueChanged(string value)
    {
        var tvalue = optionButtonSingle.Value;
        var tempstr= tvalue == "tblbillrestock" ? "采购订单" : tvalue == "tblbillreturnout" ? "采购退货" : tvalue == "tblbillswapout" ? "销售单据" : "";

        string tempval = value == "0" ? "2、按照最新"+ tempstr + "明细找回并恢复单据！" : "2、此处仅保留7天内删除单据！";
        zhuyi.Text = optionButtonSingle.Value== "tblbillswapin" ? "":"注意：1、合并的单据已被拆分，您可以恢复后重新合并！    " + tempval;
        foreach (lyTextbox textbox in Hooklist)
        {
            textbox.IsVisible = false;
        }
        if (value == "0")
        {
            Hooklist[0].IsVisible = true;
        }
        else
        {
            Hooklist[1].IsVisible = true;
        }
        mainRowList.searchByLinq(mainRowList.LinqObject, true);
        mainRowList.SortByColumns();
    }
    public void BtnResetClick()
    {
        reset = true;
        BeforeFetchingData(optionButtonSingle.Value);
    }

    /// <summary>
    /// 批量恢复
    /// </summary>
    /// <param name="control">Control.</param>
    /// <param name="mouseEvent">Mouse event.</param>
    public void OnhuifuClick(dfControl control, dfMouseEventArgs mouseEvent)
    {
        if (mainRowList.SelectedRowsIndex.Count == 0)
        {
            DBConfirmDialogUIProperty confirmUI2 = UIUtil.ConfirmDialog((YES2) => { });
            confirmUI2.ShowText = "请选中要恢复的单据！";
            confirmUI2.CancelText = "知道了";
            confirmUI2.confirmTextbox.IsVisible = false;
            confirmUI2.cancelTextbox.RelativePosition = new Vector3(83, 125, 0);
            return;
        }
        if (obsingleUpMode.Value == "1"|| optionButtonSingle.Value == "tblbillswapin")
        {
            huifuRow.DataSource.QueryName = "procedure on sp_stockscrapped_recover";
        }
        else
        {
            huifuRow.DataSource.QueryName = "procedure on sp_stockupperscrapped_recover";
        }
		if (AppManager.isSupport()) {
			DBConfirmDialogUIProperty confirmUISupport = UIUtil.ConfirmDialog((yes) =>
			                                                           {
				if (yes)
				{

						UIUtil.Toast("已成功恢复到出入库！", contentController.control, Vector3.zero, 2.5f, UIUtil.ToastAlign.CENTERTOP);
						mainRowList.DataSource.fetchData();
				}
			}, null);
			confirmUISupport.ShowText = "是否恢复选中的单据？";
			confirmUISupport.ConfirmText = "是";
			confirmUISupport.CancelText = "否";
			return;
		}
        DBConfirmDialogUIProperty confirmUI = UIUtil.ConfirmDialog((yes) =>
        {
            if (yes)
            {
                StringBuilder flowrootids = new StringBuilder();
                //  List<JsonData> removeRow = new List<JsonData>();
                foreach (int index in mainRowList.SelectedRowsIndex)
                {
                    flowrootids.Append((string)mainRowList.Value[index]["newsguid"] + ",");
                }
                string filter = flowrootids.ToString();
                filter = filter.Substring(0, filter.Length - 1);
                huifuRow.DataSource.storeProcedureParameters.Clear();
                huifuRow.DataSource.storeProcedureParameters.Add("\"" + filter + "\"");
                huifuRow.DataSource.fetchData("", (isSuccess, error) =>
                {
                    UIUtil.Toast("已成功恢复到出入库！", contentController.control, Vector3.zero, 2.5f, UIUtil.ToastAlign.CENTERTOP);
                    BeforeFetchingData(optionButtonSingle.Value);
                });
                DataUtil.instance.transaction.QueryNameRefresh("procedure on sp_stat_churuku_recover");
            }
        }, null);
        confirmUI.ShowText = "是否恢复选中的单据？";
        confirmUI.ConfirmText = "是";
        confirmUI.CancelText = "否";
    }

    void OnDestroy()
    {
        optionButtonSingle.onValueChanged -= option_ValueChanged;
        mainRowList.DataRefreshed -= RefreshFromDB;
        mainDBScrollpanel.ExpandRowCreatedEvent -= OnExpandRowCreated;
    }
}
