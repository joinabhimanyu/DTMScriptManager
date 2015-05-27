using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using DbUtility;
using System.Text;
using System.IO;
using System.Data;
using System.Data.OracleClient;
using DTMScriptManager.Models;
using GeneralServiceBL;


namespace DTMScriptManager.Controllers
{
    public class ScriptGeneratorController : Controller
    {
        //WCF Proxy Object
        private GenericServiceRef.GenericServiceClient GenService { get; set; }

        //Connection String
        private static string ConnectionString_life { get; set; }
        private static string ConnectionString { get; set; }
        public List<Table> tables = null;
        public List<Schema> schemas = null;

        public ScriptGeneratorController()
        {
            ConnectionString = System.Configuration.ConfigurationManager.AppSettings["conf"].ToString().Trim();
            DbUtility.DataObjectClass.ConnectionString = System.Configuration.ConfigurationManager.AppSettings["conf"].ToString().Trim();

            if (tables == null)
            {
                tables = new List<Table> { 
                new Table { sequence_no="1", table_name="wsdl_report_product_mst" },
                new Table { sequence_no="2", table_name="wsdl_report_genisys_wsdl" },
                new Table { sequence_no="3", table_name="wsdl_report_page_label" },
                new Table { sequence_no="4", table_name="wsdl_report_mapping_tab" },
                new Table { sequence_no="5", table_name="wsdl_report_conditional_mst" },
                new Table { sequence_no="6", table_name="wsdl_report_file_path_info" },
                new Table { sequence_no="7", table_name="wsdl_report_image_dtl" },
                new Table { sequence_no="8", table_name="wsdl_report_clause_dtl" },
                new Table { sequence_no="9", table_name="wsdl_report_mod_risk_mapping" },
                new Table { sequence_no="10", table_name="wsdl_report_risk_mapping" },
                new Table { sequence_no="11", table_name="wsdl_report_special_label" },
                new Table { sequence_no="12", table_name="wsdl_report_table_mapping" },

        };  
            }

            if (schemas == null)
            {
                DataObjectClass obj_dataobject = new DataObjectClass(ConnectionString);
                DataTable dt = null;
                string qstring = string.Empty;
                schemas = new List<Schema>();
                Schema single_schema = null;
                try
                {
                    qstring = "select username from dba_users";
                    dt = obj_dataobject.getSQLDataTable(qstring.Trim());
                    foreach (DataRow dr in dt.Rows)
                    {
                        single_schema = new Schema();
                        single_schema.schema_name = (dr[0] == System.DBNull.Value ? "" : dr[0].ToString().Trim());
                        schemas.Add(single_schema);
                        single_schema = null;
                    }
                }
                catch (Exception ex)
                {
                    
                    throw;
                }
            }
            
        }

        private void ResetSession()
        {
            Session["ProductCode"] = null;
            Session["SequenceNo"] = null;
            Session["ReportTypeCode"] = null;
            Session["HtmlTemplateNo"] = null;
            Session["TableData"] = null;
            Session["TableType"] = null;
            Session["FilePath"] = null;
            Session["FileName"] = null;
            Session["SourceSchema"] = null;
            Session["TargetSchema"] = null;
        }

        private string Authenticate(string user_id, string password)
        {
            if (user_id == null || password == null)
            {
                return "-1";
            }
            else
            {
                user_id = user_id.Trim().ToUpper();
                password = password.Trim();

                GeneralService objLVGeneralService = new GeneralService();

                if (ConnectionString != null)
                {
                    objLVGeneralService.ActualConnString = ConnectionString.Trim();
                    var auth_result = objLVGeneralService.Authenticate(user_id, password, "Wcf");
                    if (auth_result)
                    {
                        return objLVGeneralService.AuthenticationToken;
                    }
                    else
                    {
                        return "-1";
                    }
                }
                else
                {
                    objLVGeneralService.ConfigConnString = System.Configuration.ConfigurationManager.AppSettings["conf"].ToString().Trim();
                    var auth_result = objLVGeneralService.Authenticate(user_id, password, "Wcf");
                    if (auth_result)
                    {
                        ConnectionString = objLVGeneralService.ActualConnString.ToString().Trim();
                        return objLVGeneralService.AuthenticationToken;
                    }
                    else
                    {
                        return "-1";
                    }
                }
            }
        }

        [HttpPost]
        public ActionResult Login(string user_id, string password)
        {
            
            GenService = new GenericServiceRef.GenericServiceClient("EndPointHTTPGeneric", "http://172.31.247.145/WcfGenericService/GenericService.svc");

            //var authenctication_token = Authenticate(user_id.Trim(), password.Trim());
            var authenctication_token = GenService.Authenticate(user_id.Trim(), password.Trim());
            if (authenctication_token != "-1")
            {
                Session["AuthToken"] = authenctication_token.Trim();
                return View("Index");
            }
            else
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, "Invalid username or password");
            }
        }

        public ActionResult Logout()
        {
            Session["AuthToken"] = null;
            return View("Index");
        }

        public ActionResult Index()
        {
            ResetSession();
            return View();      
        }

        [HttpPost]
        public ActionResult GetSchemas(string schema_name)
        {
            var found_schema = schemas.Where(m => m.schema_name.Contains(schema_name.ToUpper().Trim()));
            if (found_schema != null)
            {
                return Json(found_schema, JsonRequestBehavior.AllowGet);
            }
            else
            {
                return new HttpNotFoundResult("No matching records found");
            }
        }

        [HttpPost]
        public ActionResult GetTableName(string table_name)
        {
            var found_table = tables.Where(m => m.table_name.Contains(table_name.Trim()));
            if (found_table != null)
            {
                return Json(found_table, JsonRequestBehavior.AllowGet);
            }
            else
            {
                return new HttpNotFoundResult("No matching records found");
            }
        }

        [HttpPost]
        public ActionResult GetTableSequenceNo(string table_name)
        {
            var found_table = tables.Where(m => m.table_name.Contains(table_name.Trim()));
            if (found_table != null)
            {
                return Json(found_table.First(), JsonRequestBehavior.AllowGet);
            }
            else
            {
                return new HttpNotFoundResult("No matching records found");
            }
        }

        public ActionResult PostFormData(int page = 1)
        {
            const int pageSize = 10;

            if (page != null && page < 1)
            {
                page = 1;
            }
            if (Session["TableData"] != null)
            {
                var type = Session["TableType"].ToString().Trim();
                switch (type)
                {
                    case "wsdl_report_product_mst":
                        var wsdl_report_product_mst_data = (IEnumerable<wsdl_report_product_mst>)Session["TableData"];
                        var wsdl_report_product_mst_trimmed_data = wsdl_report_product_mst_data.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                        var wsdl_report_product_mst_display = new PagedDataModel()
                        {
                            TotalRows = wsdl_report_product_mst_data.Count(),
                            PageSize = 10,
                            obj = wsdl_report_product_mst_trimmed_data
                        };
                        return View("RenderTable", wsdl_report_product_mst_display);

                    case "wsdl_report_genisys_wsdl":
                        var wsdl_report_genisys_wsdl_data = (IEnumerable<wsdl_report_genisys_wsdl>)Session["TableData"];
                        var wsdl_report_genisys_wsdl_trimmed_data = wsdl_report_genisys_wsdl_data.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                        var wsdl_report_genisys_wsdl_display = new PagedDataModel()
                        {
                            TotalRows = wsdl_report_genisys_wsdl_data.Count(),
                            PageSize = 10,
                            obj = wsdl_report_genisys_wsdl_trimmed_data
                        };

                        return View("RenderTable", wsdl_report_genisys_wsdl_display);

                    case "wsdl_report_page_label":
                        var wsdl_report_page_label_data = (IEnumerable<wsdl_report_page_label>)Session["TableData"];
                        var wsdl_report_page_label_trimmed_data = wsdl_report_page_label_data.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                        var wsdl_report_page_label_display = new PagedDataModel()
                        {
                            TotalRows = wsdl_report_page_label_data.Count(),
                            PageSize = 10,
                            obj = wsdl_report_page_label_trimmed_data
                        };

                        return View("RenderTable", wsdl_report_page_label_display);

                    case "wsdl_report_mapping_tab":
                        var wsdl_report_mapping_tab_data = (IEnumerable<wsdl_report_mapping_tab>)Session["TableData"];
                        var wsdl_report_mapping_tab_trimmed_data = wsdl_report_mapping_tab_data.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                        var wsdl_report_mapping_tab_display = new PagedDataModel()
                        {
                            TotalRows = wsdl_report_mapping_tab_data.Count(),
                            PageSize = 10,
                            obj = wsdl_report_mapping_tab_trimmed_data
                        };

                        return View("RenderTable", wsdl_report_mapping_tab_display);

                    case "wsdl_report_conditional_mst":
                        var wsdl_report_conditional_mst_data = (IEnumerable<wsdl_report_conditional_mst>)Session["TableData"];
                        var wsdl_report_conditional_mst_trimmed_data = wsdl_report_conditional_mst_data.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                        var wsdl_report_conditional_mst_display = new PagedDataModel()
                        {
                            TotalRows = wsdl_report_conditional_mst_data.Count(),
                            PageSize = 10,
                            obj = wsdl_report_conditional_mst_trimmed_data
                        };

                        return View("RenderTable", wsdl_report_conditional_mst_display);

                    case "wsdl_report_file_path_info":
                        var wsdl_report_file_path_info_data = (IEnumerable<wsdl_report_file_path_info>)Session["TableData"];
                        var wsdl_report_file_path_info_trimmed_data = wsdl_report_file_path_info_data.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                        var wsdl_report_file_path_info_display = new PagedDataModel()
                        {
                            TotalRows = wsdl_report_file_path_info_data.Count(),
                            PageSize = 10,
                            obj = wsdl_report_file_path_info_trimmed_data
                        };

                        return View("RenderTable", wsdl_report_file_path_info_display);

                    case "wsdl_report_image_dtl":
                        var wsdl_report_image_dtl_data = (IEnumerable<wsdl_report_image_dtl>)Session["TableData"];
                        var wsdl_report_image_dtl_trimmed_data = wsdl_report_image_dtl_data.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                        var wsdl_report_image_dtl_display = new PagedDataModel()
                        {
                            TotalRows = wsdl_report_image_dtl_data.Count(),
                            PageSize = 10,
                            obj = wsdl_report_image_dtl_trimmed_data
                        };

                        return View("RenderTable", wsdl_report_image_dtl_display);

                    case "wsdl_report_clause_dtl":
                        var wsdl_report_clause_dtl_data = (IEnumerable<wsdl_report_clause_dtl>)Session["TableData"];
                        var wsdl_report_clause_dtl_trimmed_data = wsdl_report_clause_dtl_data.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                        var wsdl_report_clause_dtl_display = new PagedDataModel()
                        {
                            TotalRows = wsdl_report_clause_dtl_data.Count(),
                            PageSize = 10,
                            obj = wsdl_report_clause_dtl_trimmed_data
                        };

                        return View("RenderTable", wsdl_report_clause_dtl_display);

                    case "wsdl_report_mod_risk_mapping":
                        var wsdl_report_mod_risk_mapping_data = (IEnumerable<wsdl_report_mod_risk_mapping>)Session["TableData"];
                        var wsdl_report_mod_risk_mapping_trimmed_data = wsdl_report_mod_risk_mapping_data.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                        var wsdl_report_mod_risk_mapping_display = new PagedDataModel()
                        {
                            TotalRows = wsdl_report_mod_risk_mapping_data.Count(),
                            PageSize = 10,
                            obj = wsdl_report_mod_risk_mapping_trimmed_data
                        };

                        return View("RenderTable", wsdl_report_mod_risk_mapping_display);

                    case "wsdl_report_risk_mapping":
                        var wsdl_report_risk_mapping_data = (IEnumerable<wsdl_report_risk_mapping>)Session["TableData"];
                        var wsdl_report_risk_mapping_trimmed_data = wsdl_report_risk_mapping_data.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                        var wsdl_report_risk_mapping_display = new PagedDataModel()
                        {
                            TotalRows = wsdl_report_risk_mapping_data.Count(),
                            PageSize = 10,
                            obj = wsdl_report_risk_mapping_trimmed_data
                        };

                        return View("RenderTable", wsdl_report_risk_mapping_display);

                    case "wsdl_report_special_label":
                        var wsdl_report_special_label_data = (IEnumerable<wsdl_report_special_label>)Session["TableData"];
                        var wsdl_report_special_label_trimmed_data = wsdl_report_special_label_data.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                        var wsdl_report_special_label_display = new PagedDataModel()
                        {
                            TotalRows = wsdl_report_special_label_data.Count(),
                            PageSize = 10,
                            obj = wsdl_report_special_label_trimmed_data
                        };

                        return View("RenderTable", wsdl_report_special_label_display);

                    case "wsdl_report_table_mapping":
                        var wsdl_report_table_mapping_data = (IEnumerable<wsdl_report_table_mapping>)Session["TableData"];
                        var wsdl_report_table_mapping_trimmed_data = wsdl_report_table_mapping_data.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                        var wsdl_report_table_mapping_display = new PagedDataModel()
                        {
                            TotalRows = wsdl_report_table_mapping_data.Count(),
                            PageSize = 10,
                            obj = wsdl_report_table_mapping_trimmed_data
                        };

                        return View("RenderTable", wsdl_report_table_mapping_display);

                    default:
                        break;
                }
            }
            return View();
        }

        [HttpPost]
        public ActionResult PostFormData(string txtSourceSchema, string txtTargetSchema, string txtTableName, string txtSequenceNo, string txtProductCode, string txtReportTypeCode, string txtHtmlTemplateNo)
        {
            DataObjectClass obj_dataobject = new DataObjectClass(ConnectionString);
            DataTable dt = null;
            string qstring = string.Empty;      

            try
            {
                int page = 1;
                const int pageSize = 10;
                ResetSession();
                Session["ProductCode"] = txtProductCode.Trim();
                Session["SequenceNo"] = txtSequenceNo.Trim();
                Session["SourceSchema"] = txtSourceSchema.Trim();
                Session["TargetSchema"] = txtTargetSchema.Trim();

                if (txtReportTypeCode.Trim() != string.Empty)
                {
                    Session["ReportTypeCode"] = txtReportTypeCode.Trim();
                }
                else
                {
                    Session["ReportTypeCode"] = string.Empty;
                }
                if (txtHtmlTemplateNo.Trim() != string.Empty)
                {
                    Session["HtmlTemplateNo"] = txtHtmlTemplateNo.Trim();
                }
                else
                {
                    Session["HtmlTemplateNo"] = string.Empty;
                }

                qstring = string.Format("select * from {0}.{1} k where k.num_product_code={2}", txtSourceSchema.Trim(), txtTableName.Trim(), txtProductCode.Trim());

                //qstring = string.Format("select * from {0} k where k.num_product_code={1}", txtTableName.Trim(), txtProductCode.Trim());

                    if (txtReportTypeCode != string.Empty)
                    {
                        qstring += string.Format(" and k.num_report_type_code={0}", txtReportTypeCode.Trim());
                    }
                    if (txtHtmlTemplateNo != string.Empty)
                    {
                        qstring += string.Format(" and k.num_html_template_no={0}", txtHtmlTemplateNo.Trim());
                    }

                    //qstring = "select * from workflow_state_master";

                    dt = obj_dataobject.getSQLDataTable(qstring.Trim());
                    if (dt != null)
                    {
                        switch (txtTableName.Trim())
                        {
                            case "wsdl_report_product_mst":
                                List<wsdl_report_product_mst> wsdl_report_product_mst_list = new List<wsdl_report_product_mst>();
                                wsdl_report_product_mst wsdl_report_product_mst_single = null;
                                foreach (DataRow dr in dt.Rows)
                                {
                                    wsdl_report_product_mst_single = new wsdl_report_product_mst();
                                    wsdl_report_product_mst_single.NUM_LINE_OF_BUSINESS = (dr[0] == System.DBNull.Value ? "" : dr[0].ToString().Trim());
                                    wsdl_report_product_mst_single.NUM_PRODUCT_CODE = (dr[1] == System.DBNull.Value ? "" : dr[1].ToString().Trim());
                                    wsdl_report_product_mst_single.TXT_PRODUCT_NAME = (dr[2] == System.DBNull.Value ? "" : dr[2].ToString().Trim());
                                    wsdl_report_product_mst_single.TXT_USER_ID = (dr[3] == System.DBNull.Value ? "" : dr[3].ToString().Trim());
                                    wsdl_report_product_mst_single.DAT_INSERT_DATE = (dr[4] == System.DBNull.Value ? "" : dr[4].ToString().Substring(0,10).Trim());
                                    wsdl_report_product_mst_list.Add(wsdl_report_product_mst_single);
                                    wsdl_report_product_mst_single = null;
                                }

                                Session["TableData"] = null;
                                Session["TableType"] = null;
                                Session["TableData"] = wsdl_report_product_mst_list;
                                Session["TableType"] = "wsdl_report_product_mst";

                                var wsdl_report_product_mst_trimmed_data = wsdl_report_product_mst_list.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                                var wsdl_report_product_mst_display = new PagedDataModel()
                                {
                                    TotalRows = wsdl_report_product_mst_list.Count(),
                                    PageSize = 10,
                                    obj = wsdl_report_product_mst_trimmed_data
                                };

                                return View("RenderTable", wsdl_report_product_mst_display);

                            case "wsdl_report_genisys_wsdl":
                                List<wsdl_report_genisys_wsdl> wsdl_report_genisys_wsdl_list = new List<wsdl_report_genisys_wsdl>();
                                wsdl_report_genisys_wsdl wsdl_report_genisys_wsdl_single = null;
                                foreach (DataRow dr in dt.Rows)
                                {
                                    wsdl_report_genisys_wsdl_single = new wsdl_report_genisys_wsdl();
                                    wsdl_report_genisys_wsdl_single.NUM_LINE_OF_BUSINESS_CODE = (dr[0] == System.DBNull.Value ? "" : dr[0].ToString().Trim());
                                    wsdl_report_genisys_wsdl_single.NUM_PRODUCT_CODE = (dr[1] == System.DBNull.Value ? "" : dr[1].ToString().Trim());
                                    wsdl_report_genisys_wsdl_single.TXT_WSDL_TYPE = (dr[2] == System.DBNull.Value ? "" : dr[2].ToString().Trim());
                                    wsdl_report_genisys_wsdl_single.TXT_SERVICE_TYPE = (dr[3] == System.DBNull.Value ? "" : dr[3].ToString().Trim());
                                    wsdl_report_genisys_wsdl_single.NUM_CONF_WSDL_TEMPLATE_NO = (dr[4] == System.DBNull.Value ? "" : dr[4].ToString().Trim());
                                    wsdl_report_genisys_wsdl_single.NUM_SERIAL_NO = (dr[5] == System.DBNull.Value ? "" : dr[5].ToString().Trim());
                                    wsdl_report_genisys_wsdl_single.NUM_CONF_PROPERTY_NO = (dr[6] == System.DBNull.Value ? "" : dr[6].ToString().Trim());
                                    wsdl_report_genisys_wsdl_single.TXT_CONF_PROPERTY_NAME = (dr[7] == System.DBNull.Value ? "" : dr[7].ToString().Trim());
                                    wsdl_report_genisys_wsdl_single.TXT_YN_HAS_CHILD = (dr[8] == System.DBNull.Value ? "" : dr[8].ToString().Trim());
                                    wsdl_report_genisys_wsdl_single.NUM_CONF_PARENT_PROPERTY_NO = (dr[9] == System.DBNull.Value ? "" : dr[9].ToString().Trim());
                                    wsdl_report_genisys_wsdl_single.TXT_CONF_CLASS_NAME = (dr[10] == System.DBNull.Value ? "" : dr[10].ToString().Trim());
                                    wsdl_report_genisys_wsdl_single.TXT_CONF_PROPERTY_DATA_TYPE = (dr[11] == System.DBNull.Value ? "" : dr[11].ToString().Trim());
                                    wsdl_report_genisys_wsdl_single.TXT_CONF_PROPERTY_INDEX = (dr[12] == System.DBNull.Value ? "" : dr[12].ToString().Trim());
                                    wsdl_report_genisys_wsdl_single.TXT_CONF_PROPERTY_PARENT_INDEX = (dr[13] == System.DBNull.Value ? "" : dr[13].ToString().Trim());
                                    wsdl_report_genisys_wsdl_single.TXT_USER_ID = (dr[14] == System.DBNull.Value ? "" : dr[14].ToString().Trim());
                                    wsdl_report_genisys_wsdl_single.DAT_INSERT_DATE = (dr[15] == System.DBNull.Value ? "" : dr[15].ToString().Substring(0,10).Trim());
                                    wsdl_report_genisys_wsdl_list.Add(wsdl_report_genisys_wsdl_single);
                                    wsdl_report_genisys_wsdl_single = null;
                                }

                                Session["TableData"] = wsdl_report_genisys_wsdl_list;
                                Session["TableType"] = "wsdl_report_genisys_wsdl";

                                var wsdl_report_genisys_wsdlt_trimmed_data = wsdl_report_genisys_wsdl_list.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                                var wsdl_report_genisys_wsdl_display = new PagedDataModel()
                                {
                                    TotalRows = wsdl_report_genisys_wsdl_list.Count(),
                                    PageSize = 10,
                                    obj = wsdl_report_genisys_wsdlt_trimmed_data
                                };

                                return View("RenderTable", wsdl_report_genisys_wsdl_display);

                            case "wsdl_report_page_label":
                                List<wsdl_report_page_label> wsdl_report_page_label_list = new List<wsdl_report_page_label>();
                                wsdl_report_page_label wsdl_report_page_label_single = null;
                                foreach (DataRow dr in dt.Rows)
                                {
                                    wsdl_report_page_label_single = new wsdl_report_page_label();
                                    wsdl_report_page_label_single.NUM_LINE_OF_BUSINESS_CODE = (dr[0] == System.DBNull.Value ? "" : dr[0].ToString().Trim());
                                    wsdl_report_page_label_single.NUM_PRODUCT_CODE = (dr[1] == System.DBNull.Value ? "" : dr[1].ToString().Trim());
                                    wsdl_report_page_label_single.TXT_SERVICE_TYPE = (dr[2] == System.DBNull.Value ? "" : dr[2].ToString().Trim());
                                    wsdl_report_page_label_single.NUM_REPORT_TYPE_CODE = (dr[3] == System.DBNull.Value ? "" : dr[3].ToString().Trim());
                                    wsdl_report_page_label_single.NUM_HTML_TEMPLATE_NO = (dr[4] == System.DBNull.Value ? "" : dr[4].ToString().Trim());
                                    wsdl_report_page_label_single.TXT_ACTUAL_PAGE_NAME = (dr[5] == System.DBNull.Value ? "" : dr[5].ToString().Trim());
                                    wsdl_report_page_label_single.NUM_LABEL_CODE = (dr[6] == System.DBNull.Value ? "" : dr[6].ToString().Trim());
                                    wsdl_report_page_label_single.TXT_LABEL_NAME = (dr[7] == System.DBNull.Value ? "" : dr[7].ToString().Trim());
                                    wsdl_report_page_label_single.TXT_USER_ID = (dr[8] == System.DBNull.Value ? "" : dr[8].ToString().Trim());
                                    wsdl_report_page_label_single.DAT_INSERT_DATE = (dr[9] == System.DBNull.Value ? "" : dr[9].ToString().Substring(0, 10).Trim());
                                    wsdl_report_page_label_list.Add(wsdl_report_page_label_single);
                                    wsdl_report_page_label_single = null;
                                }

                                Session["TableData"] = wsdl_report_page_label_list;
                                Session["TableType"] = "wsdl_report_page_label";

                                var wsdl_report_page_label_trimmed_data = wsdl_report_page_label_list.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                                var wsdl_report_page_label_display = new PagedDataModel()
                                {
                                    TotalRows = wsdl_report_page_label_list.Count(),
                                    PageSize = 10,
                                    obj = wsdl_report_page_label_trimmed_data
                                };

                                return View("RenderTable", wsdl_report_page_label_display);

                            case "wsdl_report_mapping_tab":
                                List<wsdl_report_mapping_tab> wsdl_report_mapping_tab_list = new List<wsdl_report_mapping_tab>();
                                wsdl_report_mapping_tab wsdl_report_mapping_tab_single = null;
                                foreach (DataRow dr in dt.Rows)
                                {
                                    wsdl_report_mapping_tab_single = new wsdl_report_mapping_tab();
                                    wsdl_report_mapping_tab_single.num_line_of_business_code = (dr[0] == System.DBNull.Value ? "" : dr[0].ToString().Trim());
                                    wsdl_report_mapping_tab_single.num_product_code = (dr[1] == System.DBNull.Value ? "" : dr[1].ToString().Trim());
                                    wsdl_report_mapping_tab_single.txt_service_type = (dr[2] == System.DBNull.Value ? "" : dr[2].ToString().Trim());
                                    wsdl_report_mapping_tab_single.txt_wsdl_type = (dr[3] == System.DBNull.Value ? "" : dr[3].ToString().Trim());
                                    wsdl_report_mapping_tab_single.num_report_type_code = (dr[4] == System.DBNull.Value ? "" : dr[4].ToString().Trim());
                                    wsdl_report_mapping_tab_single.num_html_template_no = (dr[5] == System.DBNull.Value ? "" : dr[5].ToString().Trim());
                                    wsdl_report_mapping_tab_single.num_conf_wsdl_template_no = (dr[6] == System.DBNull.Value ? "" : dr[6].ToString().Trim());
                                    wsdl_report_mapping_tab_single.num_serial_no = (dr[7] == System.DBNull.Value ? "" : dr[7].ToString().Trim());
                                    wsdl_report_mapping_tab_single.num_conf_property_no = (dr[8] == System.DBNull.Value ? "" : dr[8].ToString().Trim());
                                    wsdl_report_mapping_tab_single.txt_conf_property_name = (dr[9] == System.DBNull.Value ? "" : dr[9].ToString().Trim());
                                    wsdl_report_mapping_tab_single.txt_yn_has_child = (dr[10] == System.DBNull.Value ? "" : dr[10].ToString().Trim());
                                    wsdl_report_mapping_tab_single.num_conf_parent_property_no = (dr[11] == System.DBNull.Value ? "" : dr[11].ToString().Trim());
                                    wsdl_report_mapping_tab_single.txt_conf_class_name = (dr[12] == System.DBNull.Value ? "" : dr[12].ToString().Trim());
                                    wsdl_report_mapping_tab_single.txt_conf_property_data_type = (dr[13] == System.DBNull.Value ? "" : dr[13].ToString().Trim());
                                    wsdl_report_mapping_tab_single.txt_actual_page_name = (dr[14] == System.DBNull.Value ? "" : dr[14].ToString().Trim());
                                    wsdl_report_mapping_tab_single.num_label_code = (dr[15] == System.DBNull.Value ? "" : dr[15].ToString().Trim());
                                    wsdl_report_mapping_tab_single.txt_label_name = (dr[16] == System.DBNull.Value ? "" : dr[16].ToString().Trim());
                                    wsdl_report_mapping_tab_single.txt_conf_parent_property_name = (dr[17] == System.DBNull.Value ? "" : dr[17].ToString().Trim());
                                    wsdl_report_mapping_tab_single.txt_mapping_type = (dr[18] == System.DBNull.Value ? "" : dr[18].ToString().Trim());
                                    wsdl_report_mapping_tab_single.txt_yn_default_value = (dr[19] == System.DBNull.Value ? "" : dr[19].ToString().Trim());
                                    wsdl_report_mapping_tab_single.txt_default_value = (dr[20] == System.DBNull.Value ? "" : dr[20].ToString().Trim());
                                    wsdl_report_mapping_tab_single.txt_is_word = (dr[21] == System.DBNull.Value ? "" : dr[21].ToString().Trim());
                                    wsdl_report_mapping_tab_single.txt_date_format = (dr[22] == System.DBNull.Value ? "" : dr[22].ToString().Trim());
                                    wsdl_report_mapping_tab_single.txt_zero_replace = (dr[23] == System.DBNull.Value ? "" : dr[23].ToString().Trim());
                                    wsdl_report_mapping_tab_single.txt_blank_replace = (dr[24] == System.DBNull.Value ? "" : dr[24].ToString().Trim());
                                    wsdl_report_mapping_tab_single.txt_is_old_value = (dr[25] == System.DBNull.Value ? "" : dr[25].ToString().Trim());
                                    wsdl_report_mapping_tab_single.txt_rcld_component = (dr[26] == System.DBNull.Value ? "" : dr[26].ToString().Trim());
                                    wsdl_report_mapping_tab_single.txt_coll_or_direct_mapping = (dr[27] == System.DBNull.Value ? "" : dr[27].ToString().Trim());
                                    wsdl_report_mapping_tab_single.txt_user_id = (dr[28] == System.DBNull.Value ? "" : dr[28].ToString().Trim());
                                    wsdl_report_mapping_tab_single.dat_insert_date = (dr[29] == System.DBNull.Value ? "" : dr[29].ToString().Substring(0, 10).Trim());
                                    wsdl_report_mapping_tab_list.Add(wsdl_report_mapping_tab_single);
                                    wsdl_report_mapping_tab_single = null;
                                }

                                Session["TableData"] = wsdl_report_mapping_tab_list;
                                Session["TableType"] = "wsdl_report_mapping_tab";

                                var wsdl_report_mapping_tab_trimmed_data = wsdl_report_mapping_tab_list.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                                var wsdl_report_mapping_tab_display = new PagedDataModel()
                                {
                                    TotalRows = wsdl_report_mapping_tab_list.Count(),
                                    PageSize = 10,
                                    obj = wsdl_report_mapping_tab_trimmed_data
                                };

                                return View("RenderTable", wsdl_report_mapping_tab_display);

                            case "wsdl_report_conditional_mst":
                                List<wsdl_report_conditional_mst> wsdl_report_conditional_mst_list = new List<wsdl_report_conditional_mst>();
                                wsdl_report_conditional_mst wsdl_report_conditional_mst_single = null;
                                foreach (DataRow dr in dt.Rows)
                                {
                                    wsdl_report_conditional_mst_single = new wsdl_report_conditional_mst();
                                    wsdl_report_conditional_mst_single.num_line_of_business_code = (dr[0] == System.DBNull.Value ? "" : dr[0].ToString().Trim());
                                    wsdl_report_conditional_mst_single.num_product_code = (dr[1] == System.DBNull.Value ? "" : dr[1].ToString().Trim());
                                    wsdl_report_conditional_mst_single.txt_service_type = (dr[2] == System.DBNull.Value ? "" : dr[2].ToString().Trim());
                                    wsdl_report_conditional_mst_single.txt_wsdl_type = (dr[3] == System.DBNull.Value ? "" : dr[3].ToString().Trim());
                                    wsdl_report_conditional_mst_single.num_conf_wsdl_template_no = (dr[4] == System.DBNull.Value ? "" : dr[4].ToString().Trim());
                                    wsdl_report_conditional_mst_single.num_report_type_code = (dr[5] == System.DBNull.Value ? "" : dr[5].ToString().Trim());
                                    wsdl_report_conditional_mst_single.num_conf_property_no = (dr[6] == System.DBNull.Value ? "" : dr[6].ToString().Trim());
                                    wsdl_report_conditional_mst_single.txt_conf_property_name = (dr[7] == System.DBNull.Value ? "" : dr[7].ToString().Trim());
                                    wsdl_report_conditional_mst_single.num_serial_no = (dr[8] == System.DBNull.Value ? "" : dr[8].ToString().Trim());
                                    wsdl_report_conditional_mst_single.txt_original_value = (dr[9] == System.DBNull.Value ? "" : dr[9].ToString().Trim());
                                    wsdl_report_conditional_mst_single.txt_replaced_value = (dr[10] == System.DBNull.Value ? "" : dr[10].ToString().Trim());
                                    wsdl_report_conditional_mst_single.txt_user_id = (dr[11] == System.DBNull.Value ? "" : dr[11].ToString().Trim());
                                    wsdl_report_conditional_mst_single.dat_insert_date = (dr[12] == System.DBNull.Value ? "" : dr[12].ToString().Substring(0, 10).Trim());
                                    wsdl_report_conditional_mst_list.Add(wsdl_report_conditional_mst_single);
                                    wsdl_report_conditional_mst_single = null;
                                }

                                Session["TableData"] = wsdl_report_conditional_mst_list;
                                Session["TableType"] = "wsdl_report_conditional_mst";

                                var wsdl_report_conditional_mst_trimmed_data = wsdl_report_conditional_mst_list.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                                var wsdl_report_conditional_mst_display = new PagedDataModel()
                                {
                                    TotalRows = wsdl_report_conditional_mst_list.Count(),
                                    PageSize = 10,
                                    obj = wsdl_report_conditional_mst_trimmed_data
                                };

                                return View("RenderTable", wsdl_report_conditional_mst_display);

                            case "wsdl_report_file_path_info":
                                List<wsdl_report_file_path_info> wsdl_report_file_path_info_list = new List<wsdl_report_file_path_info>();
                                wsdl_report_file_path_info wsdl_report_file_path_info_single = null;
                                foreach (DataRow dr in dt.Rows)
                                {
                                    wsdl_report_file_path_info_single = new wsdl_report_file_path_info();
                                    wsdl_report_file_path_info_single.num_line_of_business_code = (dr[0] == System.DBNull.Value ? "" : dr[0].ToString().Trim());
                                    wsdl_report_file_path_info_single.num_product_code = (dr[1] == System.DBNull.Value ? "" : dr[1].ToString().Trim());
                                    wsdl_report_file_path_info_single.num_report_type_code = (dr[2] == System.DBNull.Value ? "" : dr[2].ToString().Trim());
                                    wsdl_report_file_path_info_single.num_html_template_no = (dr[3] == System.DBNull.Value ? "" : dr[3].ToString().Trim());
                                    wsdl_report_file_path_info_single.txt_source_path = (dr[4] == System.DBNull.Value ? "" : dr[4].ToString().Trim());
                                    wsdl_report_file_path_info_single.txt_destination_path = (dr[5] == System.DBNull.Value ? "" : dr[5].ToString().Trim());
                                    wsdl_report_file_path_info_single.dat_start_date = (dr[6] == System.DBNull.Value ? "" : dr[6].ToString().Substring(0, 10).Trim());
                                    wsdl_report_file_path_info_single.dat_end_date = (dr[7] == System.DBNull.Value ? "" : dr[7].ToString().Substring(0, 10).Trim());
                                    wsdl_report_file_path_info_single.txt_policy_shedule = (dr[8] == System.DBNull.Value ? "" : dr[8].ToString().Trim());
                                    wsdl_report_file_path_info_single.txt_vehicle_class_code = (dr[9] == System.DBNull.Value ? "" : dr[9].ToString().Trim());
                                    wsdl_report_file_path_info_single.txt_risk_variant = (dr[10] == System.DBNull.Value ? "" : dr[10].ToString().Trim());
                                    wsdl_report_file_path_info_single.txt_user_id = (dr[11] == System.DBNull.Value ? "" : dr[11].ToString().Trim());
                                    wsdl_report_file_path_info_single.dat_insert_date = (dr[12] == System.DBNull.Value ? "" : dr[12].ToString().Substring(0, 10).Trim());
                                    wsdl_report_file_path_info_list.Add(wsdl_report_file_path_info_single);
                                    wsdl_report_file_path_info_single = null;
                                }

                                Session["TableData"] = wsdl_report_file_path_info_list;
                                Session["TableType"] = "wsdl_report_file_path_info";

                                var wsdl_report_file_path_info_trimmed_data = wsdl_report_file_path_info_list.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                                var wsdl_report_file_path_info_display = new PagedDataModel()
                                {
                                    TotalRows = wsdl_report_file_path_info_list.Count(),
                                    PageSize = 10,
                                    obj = wsdl_report_file_path_info_trimmed_data
                                };

                                return View("RenderTable", wsdl_report_file_path_info_display);

                            case "wsdl_report_image_dtl":
                                List<wsdl_report_image_dtl> wsdl_report_image_dtl_list = new List<wsdl_report_image_dtl>();
                                wsdl_report_image_dtl wsdl_report_image_dtl_single = null;
                                foreach (DataRow dr in dt.Rows)
                                {
                                    wsdl_report_image_dtl_single = new wsdl_report_image_dtl();
                                    wsdl_report_image_dtl_single.num_line_of_business_code = (dr[0] == System.DBNull.Value ? "" : dr[0].ToString().Trim());
                                    wsdl_report_image_dtl_single.num_product_code = (dr[1] == System.DBNull.Value ? "" : dr[1].ToString().Trim());
                                    wsdl_report_image_dtl_single.txt_wsdl_type = (dr[2] == System.DBNull.Value ? "" : dr[2].ToString().Trim());
                                    wsdl_report_image_dtl_single.num_report_type_code = (dr[3] == System.DBNull.Value ? "" : dr[3].ToString().Trim());
                                    wsdl_report_image_dtl_single.num_template_no = (dr[4] == System.DBNull.Value ? "" : dr[4].ToString().Trim());
                                    wsdl_report_image_dtl_single.txt_image_type = (dr[5] == System.DBNull.Value ? "" : dr[5].ToString().Trim());
                                    wsdl_report_image_dtl_single.num_serial_no = (dr[6] == System.DBNull.Value ? "" : dr[6].ToString().Trim());
                                    wsdl_report_image_dtl_single.txt_file_path = (dr[7] == System.DBNull.Value ? "" : dr[7].ToString().Trim());
                                    wsdl_report_image_dtl_single.txt_user_id = (dr[8] == System.DBNull.Value ? "" : dr[8].ToString().Trim());
                                    wsdl_report_image_dtl_single.dat_insert_date = (dr[9] == System.DBNull.Value ? "" : dr[9].ToString().Substring(0, 10).Trim());
                                    wsdl_report_image_dtl_list.Add(wsdl_report_image_dtl_single);
                                    wsdl_report_image_dtl_single = null;
                                }

                                Session["TableData"] = wsdl_report_image_dtl_list;
                                Session["TableType"] = "wsdl_report_image_dtl";

                                var wsdl_report_image_dtl_trimmed_data = wsdl_report_image_dtl_list.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                                var wsdl_report_image_dtl_display = new PagedDataModel()
                                {
                                    TotalRows = wsdl_report_image_dtl_list.Count(),
                                    PageSize = 10,
                                    obj = wsdl_report_image_dtl_trimmed_data
                                };

                                return View("RenderTable", wsdl_report_image_dtl_display);

                            case "wsdl_report_clause_dtl":
                                List<wsdl_report_clause_dtl> wsdl_report_clause_dtl_list = new List<wsdl_report_clause_dtl>();
                                wsdl_report_clause_dtl wsdl_report_clause_dtl_single = null;
                                foreach (DataRow dr in dt.Rows)
                                {
                                    wsdl_report_clause_dtl_single = new wsdl_report_clause_dtl();
                                    wsdl_report_clause_dtl_single.num_line_of_business_code = (dr[0] == System.DBNull.Value ? "" : dr[0].ToString().Trim());
                                    wsdl_report_clause_dtl_single.num_product_code = (dr[1] == System.DBNull.Value ? "" : dr[1].ToString().Trim());
                                    wsdl_report_clause_dtl_single.txt_service_type = (dr[2] == System.DBNull.Value ? "" : dr[2].ToString().Trim());
                                    wsdl_report_clause_dtl_single.txt_wsdl_type = (dr[3] == System.DBNull.Value ? "" : dr[3].ToString().Trim());
                                    wsdl_report_clause_dtl_single.num_report_type_code = (dr[4] == System.DBNull.Value ? "" : dr[4].ToString().Trim());
                                    wsdl_report_clause_dtl_single.num_html_template_no = (dr[5] == System.DBNull.Value ? "" : dr[5].ToString().Trim());
                                    wsdl_report_clause_dtl_single.num_conf_wsdl_template_no = (dr[6] == System.DBNull.Value ? "" : dr[6].ToString().Trim());
                                    wsdl_report_clause_dtl_single.num_serial_no = (dr[7] == System.DBNull.Value ? "" : dr[7].ToString().Trim());
                                    wsdl_report_clause_dtl_single.num_label_code = (dr[8] == System.DBNull.Value ? "" : dr[8].ToString().Trim());
                                    wsdl_report_clause_dtl_single.txt_label_name = (dr[9] == System.DBNull.Value ? "" : dr[9].ToString().Trim());
                                    wsdl_report_clause_dtl_single.txt_component_type = (dr[10] == System.DBNull.Value ? "" : dr[10].ToString().Trim());
                                    wsdl_report_clause_dtl_single.num_clause_code = (dr[11] == System.DBNull.Value ? "" : dr[11].ToString().Trim());
                                    wsdl_report_clause_dtl_single.txt_conf_parent_property_name = (dr[12] == System.DBNull.Value ? "" : dr[12].ToString().Trim());
                                    wsdl_report_clause_dtl_single.num_conf_property_no = (dr[13] == System.DBNull.Value ? "" : dr[13].ToString().Trim());
                                    wsdl_report_clause_dtl_single.txt_conf_property_name = (dr[14] == System.DBNull.Value ? "" : dr[14].ToString().Trim());
                                    wsdl_report_clause_dtl_single.txt_url_path = (dr[15] == System.DBNull.Value ? "" : dr[15].ToString().Trim());
                                    wsdl_report_clause_dtl_single.txt_is_new_page = (dr[16] == System.DBNull.Value ? "" : dr[16].ToString().Trim());
                                    wsdl_report_clause_dtl_single.txt_user_id = (dr[17] == System.DBNull.Value ? "" : dr[17].ToString().Trim());
                                    wsdl_report_clause_dtl_single.dat_insert_date = (dr[18] == System.DBNull.Value ? "" : dr[18].ToString().Substring(0, 10).Trim());
                                    wsdl_report_clause_dtl_list.Add(wsdl_report_clause_dtl_single);
                                    wsdl_report_clause_dtl_single = null;
                                }

                                Session["TableData"] = wsdl_report_clause_dtl_list;
                                Session["TableType"] = "wsdl_report_clause_dtl";

                                var wsdl_report_clause_dtl_trimmed_data = wsdl_report_clause_dtl_list.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                                var wsdl_report_clause_dtl_display = new PagedDataModel()
                                {
                                    TotalRows = wsdl_report_clause_dtl_list.Count(),
                                    PageSize = 10,
                                    obj = wsdl_report_clause_dtl_trimmed_data
                                };

                                return View("RenderTable", wsdl_report_clause_dtl_display);

                            case "wsdl_report_mod_risk_mapping":
                                List<wsdl_report_mod_risk_mapping> wsdl_report_mod_risk_mapping_list = new List<wsdl_report_mod_risk_mapping>();
                                wsdl_report_mod_risk_mapping wsdl_report_mod_risk_mapping_single = null;
                                foreach (DataRow dr in dt.Rows)
                                {
                                    wsdl_report_mod_risk_mapping_single = new wsdl_report_mod_risk_mapping();
                                    wsdl_report_mod_risk_mapping_single.num_line_of_business_code = (dr[0] == System.DBNull.Value ? "" : dr[0].ToString().Trim());
                                    wsdl_report_mod_risk_mapping_single.num_product_code = (dr[1] == System.DBNull.Value ? "" : dr[1].ToString().Trim());
                                    wsdl_report_mod_risk_mapping_single.txt_service_type = (dr[2] == System.DBNull.Value ? "" : dr[2].ToString().Trim());
                                    wsdl_report_mod_risk_mapping_single.txt_wsdl_type = (dr[3] == System.DBNull.Value ? "" : dr[3].ToString().Trim());
                                    wsdl_report_mod_risk_mapping_single.num_report_type_code = (dr[4] == System.DBNull.Value ? "" : dr[4].ToString().Trim());
                                    wsdl_report_mod_risk_mapping_single.num_html_template_no = (dr[5] == System.DBNull.Value ? "" : dr[5].ToString().Trim());
                                    wsdl_report_mod_risk_mapping_single.txt_srl_no_pattern = (dr[6] == System.DBNull.Value ? "" : dr[6].ToString().Trim());
                                    wsdl_report_mod_risk_mapping_single.num_serial_no = (dr[7] == System.DBNull.Value ? "" : dr[7].ToString().Trim());
                                    wsdl_report_mod_risk_mapping_single.num_conf_property_no = (dr[8] == System.DBNull.Value ? "" : dr[8].ToString().Trim());
                                    wsdl_report_mod_risk_mapping_single.txt_conf_property_name = (dr[9] == System.DBNull.Value ? "" : dr[9].ToString().Trim());
                                    wsdl_report_mod_risk_mapping_single.txt_yn_has_child = (dr[10] == System.DBNull.Value ? "" : dr[10].ToString().Trim());
                                    wsdl_report_mod_risk_mapping_single.num_conf_parent_property_no = (dr[11] == System.DBNull.Value ? "" : dr[11].ToString().Trim());
                                    wsdl_report_mod_risk_mapping_single.txt_conf_class_name = (dr[12] == System.DBNull.Value ? "" : dr[12].ToString().Trim());
                                    wsdl_report_mod_risk_mapping_single.txt_conf_property_data_type = (dr[13] == System.DBNull.Value ? "" : dr[13].ToString().Trim());
                                    wsdl_report_mod_risk_mapping_single.num_label_code = (dr[14] == System.DBNull.Value ? "" : dr[14].ToString().Trim());
                                    wsdl_report_mod_risk_mapping_single.txt_label_name = (dr[15] == System.DBNull.Value ? "" : dr[15].ToString().Trim());
                                    wsdl_report_mod_risk_mapping_single.txt_conf_parent_property_name = (dr[16] == System.DBNull.Value ? "" : dr[16].ToString().Trim());
                                    wsdl_report_mod_risk_mapping_single.txt_mapping_type = (dr[17] == System.DBNull.Value ? "" : dr[17].ToString().Trim());
                                    wsdl_report_mod_risk_mapping_single.txt_is_old_value = (dr[18] == System.DBNull.Value ? "" : dr[18].ToString().Trim());
                                    wsdl_report_mod_risk_mapping_single.txt_risk_group_property = (dr[19] == System.DBNull.Value ? "" : dr[19].ToString().Trim());
                                    wsdl_report_mod_risk_mapping_single.txt_cover_group_property = (dr[20] == System.DBNull.Value ? "" : dr[20].ToString().Trim());
                                    wsdl_report_mod_risk_mapping_single.txt_heading = (dr[21] == System.DBNull.Value ? "" : dr[21].ToString().Trim());
                                    wsdl_report_mod_risk_mapping_single.num_display_seq_no = (dr[22] == System.DBNull.Value ? "" : dr[22].ToString().Trim());
                                    wsdl_report_mod_risk_mapping_single.txt_heading_is_bold = (dr[23] == System.DBNull.Value ? "" : dr[23].ToString().Trim());
                                    wsdl_report_mod_risk_mapping_single.txt_heading_text_aligment = (dr[24] == System.DBNull.Value ? "" : dr[24].ToString().Trim());
                                    wsdl_report_mod_risk_mapping_single.txt_cell_is_bold = (dr[25] == System.DBNull.Value ? "" : dr[25].ToString().Trim());
                                    wsdl_report_mod_risk_mapping_single.txt_cell_text_aligment = (dr[26] == System.DBNull.Value ? "" : dr[26].ToString().Trim());
                                    wsdl_report_mod_risk_mapping_single.txt_user_id = (dr[27] == System.DBNull.Value ? "" : dr[27].ToString().Trim());
                                    wsdl_report_mod_risk_mapping_single.dat_insert_date = (dr[28] == System.DBNull.Value ? "" : dr[28].ToString().Substring(0, 10).Trim());
                                    wsdl_report_mod_risk_mapping_list.Add(wsdl_report_mod_risk_mapping_single);
                                    wsdl_report_mod_risk_mapping_single = null;
                                }

                                Session["TableData"] = wsdl_report_mod_risk_mapping_list;
                                Session["TableType"] = "wsdl_report_mod_risk_mapping";

                                var wsdl_report_mod_risk_mapping_trimmed_data = wsdl_report_mod_risk_mapping_list.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                                var wsdl_report_mod_risk_mapping_display = new PagedDataModel()
                                {
                                    TotalRows = wsdl_report_mod_risk_mapping_list.Count(),
                                    PageSize = 10,
                                    obj = wsdl_report_mod_risk_mapping_trimmed_data
                                };

                                return View("RenderTable", wsdl_report_mod_risk_mapping_display);

                            case "wsdl_report_risk_mapping":
                                List<wsdl_report_risk_mapping> wsdl_report_risk_mapping_list = new List<wsdl_report_risk_mapping>();
                                wsdl_report_risk_mapping wsdl_report_risk_mapping_single = null;
                                foreach (DataRow dr in dt.Rows)
                                {
                                    wsdl_report_risk_mapping_single = new wsdl_report_risk_mapping();
                                    wsdl_report_risk_mapping_single.num_line_of_business_code = (dr[0] == System.DBNull.Value ? "" : dr[0].ToString().Trim());
                                    wsdl_report_risk_mapping_single.num_product_code = (dr[1] == System.DBNull.Value ? "" : dr[1].ToString().Trim());
                                    wsdl_report_risk_mapping_single.txt_wsdl_type = (dr[2] == System.DBNull.Value ? "" : dr[2].ToString().Trim());
                                    wsdl_report_risk_mapping_single.txt_service_type = (dr[3] == System.DBNull.Value ? "" : dr[3].ToString().Trim());
                                    wsdl_report_risk_mapping_single.num_report_type_code = (dr[4] == System.DBNull.Value ? "" : dr[4].ToString().Trim());
                                    wsdl_report_risk_mapping_single.num_html_template_no = (dr[5] == System.DBNull.Value ? "" : dr[5].ToString().Trim());
                                    wsdl_report_risk_mapping_single.num_serial_no = (dr[6] == System.DBNull.Value ? "" : dr[6].ToString().Trim());
                                    wsdl_report_risk_mapping_single.num_conf_property_no = (dr[7] == System.DBNull.Value ? "" : dr[7].ToString().Trim());
                                    wsdl_report_risk_mapping_single.txt_conf_property_name = (dr[8] == System.DBNull.Value ? "" : dr[8].ToString().Trim());
                                    wsdl_report_risk_mapping_single.txt_yn_has_child = (dr[9] == System.DBNull.Value ? "" : dr[9].ToString().Trim());
                                    wsdl_report_risk_mapping_single.num_conf_parent_property_no = (dr[10] == System.DBNull.Value ? "" : dr[10].ToString().Trim());
                                    wsdl_report_risk_mapping_single.txt_conf_class_name = (dr[11] == System.DBNull.Value ? "" : dr[11].ToString().Trim());
                                    wsdl_report_risk_mapping_single.txt_actual_page_name = (dr[12] == System.DBNull.Value ? "" : dr[12].ToString().Trim());
                                    wsdl_report_risk_mapping_single.txt_conf_property_data_type = (dr[13] == System.DBNull.Value ? "" : dr[13].ToString().Trim());
                                    wsdl_report_risk_mapping_single.num_label_code = (dr[14] == System.DBNull.Value ? "" : dr[14].ToString().Trim());
                                    wsdl_report_risk_mapping_single.txt_label_name = (dr[15] == System.DBNull.Value ? "" : dr[15].ToString().Trim());
                                    wsdl_report_risk_mapping_single.txt_conf_parent_property_name = (dr[16] == System.DBNull.Value ? "" : dr[16].ToString().Trim());
                                    wsdl_report_risk_mapping_single.txt_mapping_type = (dr[17] == System.DBNull.Value ? "" : dr[17].ToString().Trim());
                                    wsdl_report_risk_mapping_single.txt_is_word = (dr[18] == System.DBNull.Value ? "" : dr[18].ToString().Trim());
                                    wsdl_report_risk_mapping_single.txt_zero_replace = (dr[19] == System.DBNull.Value ? "" : dr[19].ToString().Trim());
                                    wsdl_report_risk_mapping_single.txt_blank_replace = (dr[20] == System.DBNull.Value ? "" : dr[20].ToString().Trim());
                                    wsdl_report_risk_mapping_single.txt_is_old_value = (dr[21] == System.DBNull.Value ? "" : dr[21].ToString().Trim());
                                    wsdl_report_risk_mapping_single.yn_risked_mapped_value = (dr[22] == System.DBNull.Value ? "" : dr[22].ToString().Trim());
                                    wsdl_report_risk_mapping_single.txt_risk_component = (dr[23] == System.DBNull.Value ? "" : dr[23].ToString().Trim());
                                    wsdl_report_risk_mapping_single.yn_cover_mapped_value = (dr[24] == System.DBNull.Value ? "" : dr[24].ToString().Trim());
                                    wsdl_report_risk_mapping_single.txt_cover_component = (dr[25] == System.DBNull.Value ? "" : dr[25].ToString().Trim());
                                    wsdl_report_risk_mapping_single.yn_ld_mapped_value = (dr[26] == System.DBNull.Value ? "" : dr[26].ToString().Trim());
                                    wsdl_report_risk_mapping_single.txt_ld_component = (dr[27] == System.DBNull.Value ? "" : dr[27].ToString().Trim());
                                    wsdl_report_risk_mapping_single.txt_risk_group_property = (dr[28] == System.DBNull.Value ? "" : dr[28].ToString().Trim());
                                    wsdl_report_risk_mapping_single.txt_cover_group_property = (dr[29] == System.DBNull.Value ? "" : dr[29].ToString().Trim());
                                    wsdl_report_risk_mapping_single.txt_ld_group_property = (dr[30] == System.DBNull.Value ? "" : dr[30].ToString().Trim());
                                    wsdl_report_risk_mapping_single.txt_rcld_component = (dr[31] == System.DBNull.Value ? "" : dr[31].ToString().Trim());
                                    wsdl_report_risk_mapping_single.txt_user_id = (dr[32] == System.DBNull.Value ? "" : dr[32].ToString().Trim());
                                    wsdl_report_risk_mapping_single.dat_insert_date = (dr[33] == System.DBNull.Value ? "" : dr[33].ToString().Substring(0, 10).Trim());
                                    wsdl_report_risk_mapping_list.Add(wsdl_report_risk_mapping_single);
                                    wsdl_report_risk_mapping_single = null;
                                }

                                Session["TableData"] = wsdl_report_risk_mapping_list;
                                Session["TableType"] = "wsdl_report_risk_mapping";

                                var wsdl_report_risk_mapping_trimmed_data = wsdl_report_risk_mapping_list.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                                var wsdl_report_risk_mapping_display = new PagedDataModel()
                                {
                                    TotalRows = wsdl_report_risk_mapping_list.Count(),
                                    PageSize = 10,
                                    obj = wsdl_report_risk_mapping_trimmed_data
                                };

                                return View("RenderTable", wsdl_report_risk_mapping_display);

                            case "wsdl_report_special_label":
                                List<wsdl_report_special_label> wsdl_report_special_label_list = new List<wsdl_report_special_label>();
                                wsdl_report_special_label wsdl_report_special_label_single = null;
                                foreach (DataRow dr in dt.Rows)
                                {
                                    wsdl_report_special_label_single = new wsdl_report_special_label();
                                    wsdl_report_special_label_single.NUM_LINE_OF_BUSINESS_CODE = (dr[0] == System.DBNull.Value ? "" : dr[0].ToString().Trim());
                                    wsdl_report_special_label_single.NUM_PRODUCT_CODE = (dr[1] == System.DBNull.Value ? "" : dr[1].ToString().Trim());
                                    wsdl_report_special_label_single.TXT_SERVICE_TYPE = (dr[2] == System.DBNull.Value ? "" : dr[2].ToString().Trim());
                                    wsdl_report_special_label_single.TXT_WSDL_TYPE = (dr[3] == System.DBNull.Value ? "" : dr[3].ToString().Trim());
                                    wsdl_report_special_label_single.NUM_REPORT_TYPE_CODE = (dr[4] == System.DBNull.Value ? "" : dr[4].ToString().Trim());
                                    wsdl_report_special_label_single.NUM_HTML_TEMPLATE_NO = (dr[5] == System.DBNull.Value ? "" : dr[5].ToString().Trim());
                                    wsdl_report_special_label_single.NUM_HTML_PATTERN_NO = (dr[6] == System.DBNull.Value ? "" : dr[6].ToString().Trim());
                                    wsdl_report_special_label_single.NUM_SERIAL_NO = (dr[7] == System.DBNull.Value ? "" : dr[7].ToString().Trim());
                                    wsdl_report_special_label_single.NUM_LABEL_CODE = (dr[8] == System.DBNull.Value ? "" : dr[8].ToString().Trim());
                                    wsdl_report_special_label_single.TXT_LABEL_NAME = (dr[9] == System.DBNull.Value ? "" : dr[9].ToString().Trim());
                                    wsdl_report_special_label_single.TXT_USER_ID = (dr[10] == System.DBNull.Value ? "" : dr[10].ToString().Trim());
                                    wsdl_report_special_label_single.DAT_INSERT_DATE = (dr[11] == System.DBNull.Value ? "" : dr[11].ToString().Substring(0, 10).Trim());
                                    wsdl_report_special_label_list.Add(wsdl_report_special_label_single);
                                    wsdl_report_special_label_single = null;
                                }

                                Session["TableData"] = wsdl_report_special_label_list;
                                Session["TableType"] = "wsdl_report_special_label";

                                var wsdl_report_special_label_trimmed_data = wsdl_report_special_label_list.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                                var wsdl_report_special_label_display = new PagedDataModel()
                                {
                                    TotalRows = wsdl_report_special_label_list.Count(),
                                    PageSize = 10,
                                    obj = wsdl_report_special_label_trimmed_data
                                };

                                return View("RenderTable", wsdl_report_special_label_display);

                            case "wsdl_report_table_mapping":
                                List<wsdl_report_table_mapping> wsdl_report_table_mapping_list = new List<wsdl_report_table_mapping>();
                                wsdl_report_table_mapping wsdl_report_table_mapping_single = null;
                                foreach (DataRow dr in dt.Rows)
                                {
                                    wsdl_report_table_mapping_single = new wsdl_report_table_mapping();
                                    wsdl_report_table_mapping_single.NUM_LINE_OF_BUSINESS_CODE = (dr[0] == System.DBNull.Value ? "" : dr[0].ToString().Trim());
                                    wsdl_report_table_mapping_single.NUM_PRODUCT_CODE = (dr[1] == System.DBNull.Value ? "" : dr[1].ToString().Trim());
                                    wsdl_report_table_mapping_single.NUM_REPORT_TYPE_CODE = (dr[2] == System.DBNull.Value ? "" : dr[2].ToString().Trim());
                                    wsdl_report_table_mapping_single.NUM_HTML_TEMPLATE_NO = (dr[3] == System.DBNull.Value ? "" : dr[3].ToString().Trim());
                                    wsdl_report_table_mapping_single.TXT_WSDL_TYPE = (dr[4] == System.DBNull.Value ? "" : dr[4].ToString().Trim());
                                    wsdl_report_table_mapping_single.NUM_SERIAL_NO = (dr[5] == System.DBNull.Value ? "" : dr[5].ToString().Trim());
                                    wsdl_report_table_mapping_single.NUM_LABEL_CODE = (dr[6] == System.DBNull.Value ? "" : dr[6].ToString().Trim());
                                    wsdl_report_table_mapping_single.TXT_LABEL_NAME = (dr[7] == System.DBNull.Value ? "" : dr[7].ToString().Trim());
                                    wsdl_report_table_mapping_single.TXT_TABLE_NAME = (dr[8] == System.DBNull.Value ? "" : dr[8].ToString().Trim());
                                    wsdl_report_table_mapping_single.NUM_COLUMN_SEQUENCE = (dr[9] == System.DBNull.Value ? "" : dr[9].ToString().Trim());
                                    wsdl_report_table_mapping_single.TXT_IS_COLUMN_VISIBLE = (dr[10] == System.DBNull.Value ? "" : dr[10].ToString().Trim());
                                    wsdl_report_table_mapping_single.TXT_COLUMN_HEADING = (dr[11] == System.DBNull.Value ? "" : dr[11].ToString().Trim());
                                    wsdl_report_table_mapping_single.TXT_HEADING_IS_BOLD = (dr[12] == System.DBNull.Value ? "" : dr[12].ToString().Trim());
                                    wsdl_report_table_mapping_single.TXT_HEADING_ALIGMENT = (dr[13] == System.DBNull.Value ? "" : dr[13].ToString().Trim());
                                    wsdl_report_table_mapping_single.TXT_HEADER_WIDTH = (dr[14] == System.DBNull.Value ? "" : dr[14].ToString().Trim());
                                    wsdl_report_table_mapping_single.TXT_RISK_COVER = (dr[15] == System.DBNull.Value ? "" : dr[15].ToString().Trim());
                                    wsdl_report_table_mapping_single.TXT_COLUMN_NAME = (dr[16] == System.DBNull.Value ? "" : dr[16].ToString().Trim());
                                    wsdl_report_table_mapping_single.TXT_DATA_TYPE = (dr[17] == System.DBNull.Value ? "" : dr[17].ToString().Trim());
                                    wsdl_report_table_mapping_single.TXT_DATA_FORMAT = (dr[18] == System.DBNull.Value ? "" : dr[18].ToString().Trim());
                                    wsdl_report_table_mapping_single.TXT_CELL_IS_BOLD = (dr[19] == System.DBNull.Value ? "" : dr[19].ToString().Trim());
                                    wsdl_report_table_mapping_single.TXT_CELL_ALIGMENT = (dr[20] == System.DBNull.Value ? "" : dr[20].ToString().Trim());
                                    wsdl_report_table_mapping_single.TXT_DEFAULT_VALUE = (dr[21] == System.DBNull.Value ? "" : dr[21].ToString().Trim());
                                    wsdl_report_table_mapping_single.TXT_WHERE_CLAUSE = (dr[22] == System.DBNull.Value ? "" : dr[22].ToString().Trim());
                                    wsdl_report_table_mapping_single.TXT_ORDER_BY = (dr[23] == System.DBNull.Value ? "" : dr[23].ToString().Trim());
                                    wsdl_report_table_mapping_single.TXT_GROUP_FUNCTION = (dr[24] == System.DBNull.Value ? "" : dr[24].ToString().Trim());
                                    wsdl_report_table_mapping_single.TXT_GROUP_BY = (dr[25] == System.DBNull.Value ? "" : dr[25].ToString().Trim());
                                    wsdl_report_table_mapping_single.TXT_DISPLAY_TYPE = (dr[26] == System.DBNull.Value ? "" : dr[26].ToString().Trim());
                                    wsdl_report_table_mapping_single.TXT_WSDL_PROPERTY = (dr[27] == System.DBNull.Value ? "" : dr[27].ToString().Trim());
                                    wsdl_report_table_mapping_single.TXT_HAS_PARENT = (dr[28] == System.DBNull.Value ? "" : dr[28].ToString().Trim());
                                    wsdl_report_table_mapping_single.TXT_WSDL_PARENT_PROPERTY = (dr[29] == System.DBNull.Value ? "" : dr[29].ToString().Trim());
                                    wsdl_report_table_mapping_single.TXT_WSDL_CONDITION_PROPERTY = (dr[30] == System.DBNull.Value ? "" : dr[30].ToString().Trim());
                                    wsdl_report_table_mapping_single.TXT_WSDL_CONDITION = (dr[31] == System.DBNull.Value ? "" : dr[31].ToString().Trim());
                                    wsdl_report_table_mapping_single.TXT_REPLACE_VALUE = (dr[32] == System.DBNull.Value ? "" : dr[32].ToString().Trim());
                                    wsdl_report_table_mapping_single.TXT_USER_ID = (dr[33] == System.DBNull.Value ? "" : dr[33].ToString().Trim());
                                    wsdl_report_table_mapping_single.DAT_INSERT_DATE = (dr[34] == System.DBNull.Value ? "" : dr[34].ToString().Substring(0, 10).Trim());
                                    wsdl_report_table_mapping_list.Add(wsdl_report_table_mapping_single);
                                    wsdl_report_table_mapping_single = null;
                                }

                                Session["TableData"] = wsdl_report_table_mapping_list;
                                Session["TableType"] = "wsdl_report_table_mapping";

                                var wsdl_report_table_mapping_trimmed_data = wsdl_report_table_mapping_list.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                                var wsdl_report_table_mapping_display = new PagedDataModel()
                                {
                                    TotalRows = wsdl_report_table_mapping_list.Count(),
                                    PageSize = 10,
                                    obj = wsdl_report_table_mapping_trimmed_data
                                };

                                return View("RenderTable", wsdl_report_table_mapping_display);

                            default:
                                break;
                        }
                    }
                }

                
            catch (Exception ex)
            {

                return new HttpStatusCodeResult(System.Net.HttpStatusCode.ExpectationFailed);
            }
            return Json(dt, JsonRequestBehavior.AllowGet);
        }

        public ActionResult Download()
        {
            var file_path = string.Empty;
            var file_name = string.Empty;

            string result = GenerateScript();
            if (result == "success")
            {
                    file_path = Session["FilePath"].ToString().Trim();
                    file_name = Session["FileName"].ToString().Trim();

                    byte[] fileBytes = System.IO.File.ReadAllBytes(file_path);

                    return File(fileBytes, System.Net.Mime.MediaTypeNames.Application.Octet, file_name);              
            }
            else
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.ExpectationFailed);
            }
            
        }

        private string GenerateScript()
        {
            StringBuilder str_builder = new StringBuilder();
            StreamWriter str_writer = null;
            string output_path = string.Empty;
            string file_path = string.Empty;
            string result = string.Empty;

            try
            {
                output_path = Server.MapPath("~/GeneratedScripts");
                if (Directory.Exists(output_path.Trim()) == false)
                {
                    Directory.CreateDirectory(output_path.Trim());
                }
                else
                {
                    string[] files = Directory.GetFiles(output_path.Trim());
                    foreach (var item in files)
                    {
                        System.IO.File.Delete(item);
                    }
                    Directory.Delete(output_path.Trim());
                    Directory.CreateDirectory(output_path.Trim());
                }

                var table_type = Session["TableType"].ToString().Trim();
                var product_code = Session["ProductCode"].ToString().Trim();
                var sequence_no = Session["SequenceNo"].ToString().Trim();
                var report_type_code = Session["ReportTypeCode"].ToString().Trim();
                var html_template_no = Session["HtmlTemplateNo"].ToString().Trim();
                var target_schema = Session["TargetSchema"].ToString().Trim();

                switch (table_type)
                {
                    case "wsdl_report_product_mst":

                        var wsdl_report_product_mst_data = (IEnumerable<wsdl_report_product_mst>)Session["TableData"];

                        str_builder.Append(string.Format("delete from {0}.wsdl_report_table_mapping k where k.num_product_code={1}",target_schema.Trim(), product_code.Trim()));

                        if (report_type_code != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_report_type_code={0}", report_type_code.Trim()));
                        }

                        if (html_template_no != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_html_template_no={0}", html_template_no.Trim()));
                        }

                        str_builder.Append(";");
                        str_builder.Append(System.Environment.NewLine);

                        str_builder.Append(string.Format("delete from {0}.wsdl_report_special_label k where k.num_product_code={1}", target_schema.Trim(), product_code.Trim()));

                        if (report_type_code != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_report_type_code={0}", report_type_code.Trim()));
                        }

                        if (html_template_no != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_html_template_no={0}", html_template_no.Trim()));
                        }

                        str_builder.Append(";");
                        str_builder.Append(System.Environment.NewLine);

                        str_builder.Append(string.Format("delete from {0}.wsdl_report_risk_mapping k where k.num_product_code={1}", target_schema.Trim(), product_code.Trim()));

                        if (report_type_code != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_report_type_code={0}", report_type_code.Trim()));
                        }

                        if (html_template_no != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_html_template_no={0}", html_template_no.Trim()));
                        }

                        str_builder.Append(";");
                        str_builder.Append(System.Environment.NewLine);

                        str_builder.Append(string.Format("delete from {0}.wsdl_report_mod_risk_mapping k where k.num_product_code={1}", target_schema.Trim(), product_code.Trim()));

                        if (report_type_code != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_report_type_code={0}", report_type_code.Trim()));
                        }

                        if (html_template_no != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_html_template_no={0}", html_template_no.Trim()));
                        }

                        str_builder.Append(";");
                        str_builder.Append(System.Environment.NewLine);

                        str_builder.Append(string.Format("delete from {0}.wsdl_report_clause_dtl k where k.num_product_code={1}", target_schema.Trim(), product_code.Trim()));

                        if (report_type_code != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_report_type_code={0}", report_type_code.Trim()));
                        }

                        if (html_template_no != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_html_template_no={0}", html_template_no.Trim()));
                        }

                        str_builder.Append(";");
                        str_builder.Append(System.Environment.NewLine);

                        str_builder.Append(string.Format("delete from {0}.wsdl_report_image_dtl k where k.num_product_code={1}", target_schema.Trim(), product_code.Trim()));

                        if (report_type_code != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_report_type_code={0}", report_type_code.Trim()));
                        }

                        if (html_template_no != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_html_template_no={0}", html_template_no.Trim()));
                        }

                        str_builder.Append(";");
                        str_builder.Append(System.Environment.NewLine);

                        str_builder.Append(string.Format("delete from {0}.wsdl_report_file_path_info k where k.num_product_code={1}", target_schema.Trim(), product_code.Trim()));

                        if (report_type_code != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_report_type_code={0}", report_type_code.Trim()));
                        }

                        if (html_template_no != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_html_template_no={0}", html_template_no.Trim()));
                        }

                        str_builder.Append(";");
                        str_builder.Append(System.Environment.NewLine);

                        str_builder.Append(string.Format("delete from {0}.wsdl_report_conditional_mst k where k.num_product_code={1}", target_schema.Trim(), product_code.Trim()));

                        if (report_type_code != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_report_type_code={0}", report_type_code.Trim()));
                        }

                        if (html_template_no != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_html_template_no={0}", html_template_no.Trim()));
                        }

                        str_builder.Append(";");
                        str_builder.Append(System.Environment.NewLine);

                        str_builder.Append(string.Format("delete from {0}.wsdl_report_mapping_tab k where k.num_product_code={1}", target_schema.Trim(), product_code.Trim()));

                        if (report_type_code != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_report_type_code={0}", report_type_code.Trim()));
                        }

                        if (html_template_no != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_html_template_no={0}", html_template_no.Trim()));
                        }

                        str_builder.Append(";");
                        str_builder.Append(System.Environment.NewLine);

                        str_builder.Append(string.Format("delete from {0}.wsdl_report_page_label k where k.num_product_code={1}", target_schema.Trim(), product_code.Trim()));

                        if (report_type_code != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_report_type_code={0}", report_type_code.Trim()));
                        }

                        if (html_template_no != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_html_template_no={0}", html_template_no.Trim()));
                        }

                        str_builder.Append(";");
                        str_builder.Append(System.Environment.NewLine);

                        str_builder.Append(string.Format("delete from {0}.wsdl_report_genisys_wsdl k where k.num_product_code={1}", target_schema.Trim(), product_code.Trim()));

                        if (report_type_code != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_report_type_code={0}", report_type_code.Trim()));
                        }

                        if (html_template_no != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_html_template_no={0}", html_template_no.Trim()));
                        }

                        str_builder.Append(";");
                        str_builder.Append(System.Environment.NewLine);

                        str_builder.Append(string.Format("delete from {0}.{1} k where k.num_product_code={2}", target_schema.Trim(), table_type.Trim(), product_code.Trim()));

                        if (report_type_code != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_report_type_code={0}", report_type_code.Trim()));
                        }

                        if (html_template_no != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_html_template_no={0}", html_template_no.Trim()));
                        }

                        str_builder.Append(";");
                        
                        str_builder.Append(System.Environment.NewLine);
                        str_builder.Append("commit;");
                        str_builder.Append(System.Environment.NewLine);

                        foreach (var item in wsdl_report_product_mst_data)
                        {
                            str_builder.Append(string.Format("insert into {0}.{1} (NUM_LINE_OF_BUSINESS, NUM_PRODUCT_CODE, TXT_PRODUCT_NAME, TXT_USER_ID," +
                                " DAT_INSERT_DATE) values ('{2}', '{3}', '{4}', '{5}', to_date('{6}','DD-MM-RRRR'));", target_schema.Trim(), table_type, item.NUM_LINE_OF_BUSINESS, item.NUM_PRODUCT_CODE, item.TXT_PRODUCT_NAME,
                                item.TXT_USER_ID, item.DAT_INSERT_DATE));

                            str_builder.Append(System.Environment.NewLine);
                        }

                        str_builder.Append("commit;");

                        break;
                    case "wsdl_report_genisys_wsdl":
                        var wsdl_report_genisys_wsdl_data = (IEnumerable<wsdl_report_genisys_wsdl>)Session["TableData"];

                        str_builder.Append(string.Format("delete from {0}.{1} k where k.num_product_code={2}", target_schema.Trim(), table_type.Trim(), product_code.Trim()));

                        if (report_type_code != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_report_type_code={0}", report_type_code.Trim()));
                        }

                        if (html_template_no != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_html_template_no={0}", html_template_no.Trim()));
                        }

                        str_builder.Append(";");
                        str_builder.Append(System.Environment.NewLine);
                        str_builder.Append("commit;");
                        str_builder.Append(System.Environment.NewLine);

                        foreach (var item in wsdl_report_genisys_wsdl_data)
                        {
                            str_builder.Append(string.Format("insert into {0}.{1} (NUM_LINE_OF_BUSINESS_CODE, NUM_PRODUCT_CODE, TXT_WSDL_TYPE, TXT_SERVICE_TYPE," +
                                " NUM_CONF_WSDL_TEMPLATE_NO, NUM_SERIAL_NO, NUM_CONF_PROPERTY_NO, TXT_CONF_PROPERTY_NAME, TXT_YN_HAS_CHILD," +
                                " NUM_CONF_PARENT_PROPERTY_NO, TXT_CONF_CLASS_NAME	, TXT_CONF_PROPERTY_DATA_TYPE, TXT_CONF_PROPERTY_INDEX," +
                                " TXT_CONF_PROPERTY_PARENT_INDEX, TXT_USER_ID, DAT_INSERT_DATE) values ('{2}', '{3}', '{4}', '{5}', '{6}', '{7}', '{8}', '{9}', '{10}'" +
                                ", '{11}', '{12}', '{13}', '{14}', '{15}', '{16}', to_date('{17}','DD-MM-RRRR'));", target_schema.Trim(), table_type, item.NUM_LINE_OF_BUSINESS_CODE, item.NUM_PRODUCT_CODE, item.TXT_WSDL_TYPE,
                                item.TXT_SERVICE_TYPE, item.NUM_CONF_WSDL_TEMPLATE_NO, item.NUM_SERIAL_NO, item.NUM_CONF_PROPERTY_NO, item.TXT_CONF_PROPERTY_NAME,
                                item.TXT_YN_HAS_CHILD, item.NUM_CONF_PARENT_PROPERTY_NO, item.TXT_CONF_CLASS_NAME, item.TXT_CONF_PROPERTY_DATA_TYPE, item.TXT_CONF_PROPERTY_INDEX,
                                item.TXT_CONF_PROPERTY_PARENT_INDEX, item.TXT_USER_ID, item.DAT_INSERT_DATE));

                            str_builder.Append(System.Environment.NewLine);
                        }

                        str_builder.Append("commit;");

                        break;
                    case "wsdl_report_page_label":

                        var wsdl_report_page_label_data = (IEnumerable<wsdl_report_page_label>)Session["TableData"];

                        str_builder.Append(string.Format("delete from {0}.{1} k where k.num_product_code={2}", target_schema.Trim(), table_type.Trim(), product_code.Trim()));

                        if (report_type_code != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_report_type_code={0}", report_type_code.Trim()));
                        }

                        if (html_template_no != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_html_template_no={0}", html_template_no.Trim()));
                        }

                        str_builder.Append(";");
                        str_builder.Append(System.Environment.NewLine);
                        str_builder.Append("commit;");
                        str_builder.Append(System.Environment.NewLine);

                        foreach (var item in wsdl_report_page_label_data)
                        {
                            str_builder.Append(string.Format("insert into {0}.{1} (NUM_LINE_OF_BUSINESS_CODE, NUM_PRODUCT_CODE, TXT_SERVICE_TYPE, NUM_REPORT_TYPE_CODE," +
                                " NUM_HTML_TEMPLATE_NO, TXT_ACTUAL_PAGE_NAME, NUM_LABEL_CODE, TXT_LABEL_NAME, TXT_USER_ID, DAT_INSERT_DATE)" +
                                " values ('{2}', '{3}', '{4}', '{5}', '{6}', '{7}', '{8}', '{9}', '{10}'" +
                                ", to_date('{11}','DD-MM-RRRR'));", target_schema.Trim(), table_type, item.NUM_LINE_OF_BUSINESS_CODE, item.NUM_PRODUCT_CODE, item.TXT_SERVICE_TYPE,
                                item.NUM_REPORT_TYPE_CODE, item.NUM_HTML_TEMPLATE_NO, item.TXT_ACTUAL_PAGE_NAME, item.NUM_LABEL_CODE, item.TXT_LABEL_NAME,
                                item.TXT_USER_ID, item.DAT_INSERT_DATE));

                            str_builder.Append(System.Environment.NewLine);
                        }

                        str_builder.Append("commit;");

                        break;
                    case "wsdl_report_mapping_tab":

                        var wsdl_report_mapping_tab_data = (IEnumerable<wsdl_report_mapping_tab>)Session["TableData"];

                        str_builder.Append(string.Format("delete from {0}.{1} k where k.num_product_code={2}",target_schema.Trim(), table_type.Trim(), product_code.Trim()));

                        if (report_type_code != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_report_type_code={0}", report_type_code.Trim()));
                        }

                        if (html_template_no != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_html_template_no={0}", html_template_no.Trim()));
                        }

                        str_builder.Append(";");
                        str_builder.Append(System.Environment.NewLine);
                        str_builder.Append("commit;");
                        str_builder.Append(System.Environment.NewLine);

                        foreach (var item in wsdl_report_mapping_tab_data)
                        {
                            str_builder.Append(string.Format("insert into {0}.{1} (num_line_of_business_code, num_product_code, txt_service_type, txt_wsdl_type," +
                                " num_report_type_code, num_html_template_no, num_conf_wsdl_template_no, num_serial_no, num_conf_property_no, txt_conf_property_name," +
                                " txt_yn_has_child, num_conf_parent_property_no, txt_conf_class_name, txt_conf_property_data_type, txt_actual_page_name," +
                                " num_label_code, txt_label_name, txt_conf_parent_property_name, txt_mapping_type, txt_yn_default_value, txt_default_value," +
                                " txt_is_word, txt_date_format, txt_zero_replace, txt_blank_replace, txt_is_old_value, txt_rcld_component, txt_coll_or_direct_mapping," +
                                " txt_user_id, dat_insert_date)" +
                                " values ('{2}', '{3}', '{4}', '{5}', '{6}', '{7}', '{8}', '{9}', '{10}'" +
                                ", '{11}', '{12}', '{13}', '{14}', '{15}', '{16}', '{17}', '{18}', '{19}', '{20}', '{21}', '{22}', '{23}', '{24}', '{25}', '{26}'" +
                                ", '{27}', '{28}', '{29}', '{30}', to_date('{31}','DD-MM-RRRR'));",target_schema.Trim(), table_type, item.num_line_of_business_code, item.num_product_code, item.txt_service_type,
                                item.txt_wsdl_type, item.num_report_type_code, item.num_html_template_no, item.num_conf_wsdl_template_no, item.num_serial_no,
                                item.num_conf_property_no, item.txt_conf_property_name, item.txt_yn_has_child, item.num_conf_parent_property_no, item.txt_conf_class_name,
                                item.txt_conf_property_data_type, item.txt_actual_page_name, item.num_label_code, item.txt_label_name, item.txt_conf_parent_property_name,
                                item.txt_mapping_type, item.txt_yn_default_value, item.txt_default_value, item.txt_is_word, item.txt_date_format, item.txt_zero_replace,
                                item.txt_blank_replace, item.txt_is_old_value, item.txt_rcld_component, item.txt_coll_or_direct_mapping, item.txt_user_id, item.dat_insert_date));

                            str_builder.Append(System.Environment.NewLine);
                        }

                        str_builder.Append("commit;");

                        break;
                    case "wsdl_report_conditional_mst":

                        var wsdl_report_conditional_mst_data = (IEnumerable<wsdl_report_conditional_mst>)Session["TableData"];

                        str_builder.Append(string.Format("delete from {0}.{1} k where k.num_product_code={2}",target_schema.Trim(), table_type.Trim(), product_code.Trim()));

                        if (report_type_code != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_report_type_code={0}", report_type_code.Trim()));
                        }

                        if (html_template_no != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_html_template_no={0}", html_template_no.Trim()));
                        }

                        str_builder.Append(";");
                        str_builder.Append(System.Environment.NewLine);
                        str_builder.Append("commit;");
                        str_builder.Append(System.Environment.NewLine);

                        foreach (var item in wsdl_report_conditional_mst_data)
                        {
                            str_builder.Append(string.Format("insert into {0}.{1} (num_line_of_business_code, num_product_code, txt_service_type, txt_wsdl_type," +
                                " num_conf_wsdl_template_no, num_report_type_code, num_conf_property_no, txt_conf_property_name, num_serial_no, txt_original_value," +
                                " txt_replaced_value, txt_user_id, dat_insert_date)" +
                                " values ('{2}', '{3}', '{4}', '{5}', '{6}', '{7}', '{8}', '{9}', '{10}'" +
                                ", '{11}', '{12}', '{13}', to_date('{14}','DD-MM-RRRR'));",target_schema.Trim(), table_type, item.num_line_of_business_code, item.num_product_code, item.txt_service_type,
                                item.txt_wsdl_type, item.num_conf_wsdl_template_no, item.num_report_type_code, item.num_conf_property_no, item.txt_conf_property_name,
                                item.num_serial_no, item.txt_original_value, item.txt_replaced_value, item.txt_user_id, item.dat_insert_date));

                            str_builder.Append(System.Environment.NewLine);
                        }

                        str_builder.Append("commit;");

                        break;
                    case "wsdl_report_file_path_info":
                        var wsdl_report_file_path_info_data = (IEnumerable<wsdl_report_file_path_info>)Session["TableData"];

                        str_builder.Append(string.Format("delete from {0}.{1} k where k.num_product_code={2}",target_schema.Trim(), table_type.Trim(), product_code.Trim()));

                        if (report_type_code != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_report_type_code={0}", report_type_code.Trim()));
                        }

                        if (html_template_no != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_html_template_no={0}", html_template_no.Trim()));
                        }

                        str_builder.Append(";");
                        str_builder.Append(System.Environment.NewLine);
                        str_builder.Append("commit;");
                        str_builder.Append(System.Environment.NewLine);

                        foreach (var item in wsdl_report_file_path_info_data)
                        {
                            str_builder.Append(string.Format("insert into {14}.{0} (num_line_of_business_code, num_product_code, num_report_type_code, num_html_template_no," +
                                " txt_source_path, txt_destination_path, dat_start_date, dat_end_date, txt_policy_shedule," +
                                " txt_vehicle_class_code, txt_risk_variant, txt_user_id, dat_insert_date)" +
                                " values ('{1}', '{2}', '{3}', '{4}', '{5}', '{6}', to_date('{7}','DD-MM-RRRR'), to_date('{8}','DD-MM-RRRR'), '{9}'" +
                                ", '{10}', '{11}', '{12}', to_date('{13}','DD-MM-RRRR'));", table_type, item.num_line_of_business_code, item.num_product_code, item.num_report_type_code,
                                item.num_html_template_no, item.txt_source_path, item.txt_destination_path, item.dat_start_date, item.dat_end_date,
                                item.txt_policy_shedule, item.txt_vehicle_class_code, item.txt_risk_variant, item.txt_user_id, item.dat_insert_date,target_schema.Trim()));

                            str_builder.Append(System.Environment.NewLine);
                        }

                        str_builder.Append("commit;");

                        break;
                    case "wsdl_report_image_dtl":

                        var wsdl_report_image_dtl_data = (IEnumerable<wsdl_report_image_dtl>)Session["TableData"];

                        str_builder.Append(string.Format("delete from {2}.{0} k where k.num_product_code={1}", table_type.Trim(), product_code.Trim(), target_schema.Trim()));

                        if (report_type_code != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_report_type_code={0}", report_type_code.Trim()));
                        }

                        if (html_template_no != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_html_template_no={0}", html_template_no.Trim()));
                        }

                        str_builder.Append(";");
                        str_builder.Append(System.Environment.NewLine);
                        str_builder.Append("commit;");
                        str_builder.Append(System.Environment.NewLine);

                        foreach (var item in wsdl_report_image_dtl_data)
                        {
                            str_builder.Append(string.Format("insert into {11}.{0} (num_line_of_business_code, num_product_code, txt_wsdl_type, num_report_type_code," +
                                " num_template_no, txt_image_type, num_serial_no, txt_file_path, txt_user_id," +
                                " dat_insert_date)" +
                                " values ('{1}', '{2}', '{3}', '{4}', '{5}', '{6}', '{7}', '{8}', '{9}'" +
                                ", to_date('{10}','DD-MM-RRRR'));", table_type, item.num_line_of_business_code, item.num_product_code, item.txt_wsdl_type,
                                item.num_report_type_code, item.num_template_no, item.txt_image_type, item.num_serial_no, item.txt_file_path,
                                item.txt_user_id, item.dat_insert_date, target_schema.Trim()));

                            str_builder.Append(System.Environment.NewLine);
                        }

                        str_builder.Append("commit;");

                        break;
                    case "wsdl_report_clause_dtl":

                        var wsdl_report_clause_dtl_data = (IEnumerable<wsdl_report_clause_dtl>)Session["TableData"];

                        str_builder.Append(string.Format("delete from {2}.{0} k where k.num_product_code={1}", table_type.Trim(), product_code.Trim(), target_schema.Trim()));

                        if (report_type_code != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_report_type_code={0}", report_type_code.Trim()));
                        }

                        if (html_template_no != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_html_template_no={0}", html_template_no.Trim()));
                        }

                        str_builder.Append(";");
                        str_builder.Append(System.Environment.NewLine);
                        str_builder.Append("commit;");
                        str_builder.Append(System.Environment.NewLine);

                        foreach (var item in wsdl_report_clause_dtl_data)
                        {
                            str_builder.Append(string.Format("insert into {20}.{0} (num_line_of_business_code, num_product_code, txt_service_type, txt_wsdl_type," +
                                " num_report_type_code, num_html_template_no, num_conf_wsdl_template_no, num_serial_no, num_label_code," +
                                " txt_label_name, txt_component_type, num_clause_code, txt_conf_parent_property_name, num_conf_property_no, txt_conf_property_name," +
                                " txt_url_path, txt_is_new_page, txt_user_id, dat_insert_date)" +
                                " values ('{1}', '{2}', '{3}', '{4}', '{5}', '{6}', '{7}', '{8}', '{9}'" +
                                ", '{10}', '{11}', '{12}', '{13}', '{14}', '{15}', '{16}', '{17}', '{18}', to_date('{19}','DD-MM-RRRR'));", table_type, item.num_line_of_business_code, item.num_product_code, item.txt_service_type,
                                item.txt_wsdl_type, item.num_report_type_code, item.num_html_template_no, item.num_conf_wsdl_template_no, item.num_serial_no,
                                item.num_label_code, item.txt_label_name, item.txt_component_type, item.num_clause_code, item.txt_conf_parent_property_name,
                                item.num_conf_property_no, item.txt_conf_property_name, item.txt_url_path, item.txt_is_new_page, item.txt_user_id, item.dat_insert_date, target_schema.Trim()));

                            str_builder.Append(System.Environment.NewLine);
                        }

                        str_builder.Append("commit;");

                        break;
                    case "wsdl_report_mod_risk_mapping":

                        var wsdl_report_mod_risk_mapping_data = (IEnumerable<wsdl_report_mod_risk_mapping>)Session["TableData"];

                        str_builder.Append(string.Format("delete from {2}.{0} k where k.num_product_code={1}", table_type.Trim(), product_code.Trim(), target_schema.Trim()));

                        if (report_type_code != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_report_type_code={0}", report_type_code.Trim()));
                        }

                        if (html_template_no != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_html_template_no={0}", html_template_no.Trim()));
                        }

                        str_builder.Append(";");
                        str_builder.Append(System.Environment.NewLine);
                        str_builder.Append("commit;");
                        str_builder.Append(System.Environment.NewLine);

                        foreach (var item in wsdl_report_mod_risk_mapping_data)
                        {
                            str_builder.Append(string.Format("insert into {30}.{0} (num_line_of_business_code, num_product_code, txt_service_type, txt_wsdl_type," +
                                " num_report_type_code, num_html_template_no, txt_srl_no_pattern, num_serial_no, num_conf_property_no," +
                                " txt_conf_property_name, txt_yn_has_child, num_conf_parent_property_no, txt_conf_class_name, txt_conf_property_data_type, num_label_code," +
                                " txt_label_name, txt_conf_parent_property_name, txt_mapping_type, txt_is_old_value, txt_risk_group_property, txt_cover_group_property," +
                                " txt_heading, num_display_seq_no, txt_heading_is_bold, txt_heading_text_aligment, txt_cell_is_bold, txt_cell_text_aligment, txt_user_id, dat_insert_date)" +
                                " values ('{1}', '{2}', '{3}', '{4}', '{5}', '{6}', '{7}', '{8}', '{9}'" +
                                ", '{10}', '{11}', '{12}', '{13}', '{14}', '{15}', '{16}', '{17}', '{18}', '{19}', '{20}', '{21}', '{22}', '{23}', '{24}', '{25}', '{26}'" +
                                ", '{27}', '{28}', to_date('{29}','DD-MM-RRRR'));", table_type, item.num_line_of_business_code, item.num_product_code, item.txt_service_type,
                                item.txt_wsdl_type, item.num_report_type_code, item.num_html_template_no, item.txt_srl_no_pattern, item.num_serial_no,
                                item.num_conf_property_no, item.txt_conf_property_name, item.txt_yn_has_child, item.num_conf_parent_property_no, item.txt_conf_class_name,
                                item.txt_conf_property_data_type, item.num_label_code, item.txt_label_name, item.txt_conf_parent_property_name, item.txt_mapping_type, item.txt_is_old_value,
                                item.txt_risk_group_property, item.txt_cover_group_property, item.txt_heading, item.num_display_seq_no, item.txt_heading_is_bold, item.txt_heading_text_aligment,
                                item.txt_cell_is_bold, item.txt_cell_text_aligment, item.txt_user_id, item.dat_insert_date, target_schema.Trim()));

                            str_builder.Append(System.Environment.NewLine);
                        }

                        str_builder.Append("commit;");

                        break;
                    case "wsdl_report_risk_mapping":

                        var wsdl_report_risk_mapping_data = (IEnumerable<wsdl_report_risk_mapping>)Session["TableData"];

                        str_builder.Append(string.Format("delete from {2}.{0} k where k.num_product_code={1}", table_type.Trim(), product_code.Trim(), target_schema.Trim()));

                        if (report_type_code != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_report_type_code={0}", report_type_code.Trim()));
                        }

                        if (html_template_no != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_html_template_no={0}", html_template_no.Trim()));
                        }

                        str_builder.Append(";");
                        str_builder.Append(System.Environment.NewLine);
                        str_builder.Append("commit;");
                        str_builder.Append(System.Environment.NewLine);

                        foreach (var item in wsdl_report_risk_mapping_data)
                        {
                            str_builder.Append(string.Format("insert into {35}.{0} (num_line_of_business_code, num_product_code, txt_wsdl_type, txt_service_type," +
                                " num_report_type_code, num_html_template_no, num_serial_no, num_conf_property_no, txt_conf_property_name," +
                                " txt_yn_has_child, num_conf_parent_property_no, txt_conf_class_name, txt_actual_page_name, txt_conf_property_data_type, num_label_code," +
                                " txt_label_name, txt_conf_parent_property_name, txt_mapping_type, txt_is_word, txt_zero_replace, txt_blank_replace, txt_is_old_value," +
                                " yn_risked_mapped_value, txt_risk_component, yn_cover_mapped_value, txt_cover_component, yn_ld_mapped_value, txt_ld_component," +
                                " txt_risk_group_property ,txt_cover_group_property, txt_ld_group_property, txt_rcld_component, txt_user_id, dat_insert_date)" +
                                " values ('{1}', '{2}', '{3}', '{4}', '{5}', '{6}', '{7}', '{8}', '{9}'" +
                                ", '{10}', '{11}', '{12}', '{13}', '{14}', '{15}', '{16}', '{17}', '{18}', '{19}', '{20}', '{21}', '{22}', '{23}', '{24}', '{25}', '{26}'" +
                                ", '{27}', '{28}', '{29}', '{30}', '{31}', '{32}', '{33}', to_date('{34}','DD-MM-RRRR'));", table_type, item.num_line_of_business_code, item.num_product_code, item.txt_wsdl_type,
                                item.txt_service_type, item.num_report_type_code, item.num_html_template_no, item.num_serial_no, item.num_conf_property_no,
                                item.txt_conf_property_name, item.txt_yn_has_child, item.num_conf_parent_property_no, item.txt_conf_class_name, item.txt_actual_page_name,
                                item.txt_conf_property_data_type, item.num_label_code, item.txt_label_name, item.txt_conf_parent_property_name, item.txt_mapping_type, item.txt_is_word,
                                item.txt_zero_replace, item.txt_blank_replace, item.txt_is_old_value, item.yn_risked_mapped_value, item.txt_risk_component, item.yn_cover_mapped_value,
                                item.txt_cover_component, item.yn_ld_mapped_value, item.txt_ld_component, item.txt_risk_group_property, item.txt_cover_group_property, item.txt_ld_group_property,
                                item.txt_rcld_component, item.txt_user_id, item.dat_insert_date, target_schema.Trim()));

                            str_builder.Append(System.Environment.NewLine);
                        }

                        str_builder.Append("commit;");

                        break;
                    case "wsdl_report_special_label":

                        var wsdl_report_special_label_data = (IEnumerable<wsdl_report_special_label>)Session["TableData"];

                        str_builder.Append(string.Format("delete from {2}.{0} k where k.num_product_code={1}", table_type.Trim(), product_code.Trim(), target_schema.Trim()));

                        if (report_type_code != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_report_type_code={0}", report_type_code.Trim()));
                        }

                        if (html_template_no != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_html_template_no={0}", html_template_no.Trim()));
                        }

                        str_builder.Append(";");
                        str_builder.Append(System.Environment.NewLine);
                        str_builder.Append("commit;");
                        str_builder.Append(System.Environment.NewLine);

                        foreach (var item in wsdl_report_special_label_data)
                        {
                            str_builder.Append(string.Format("insert into {13}.{0} (NUM_LINE_OF_BUSINESS_CODE, NUM_PRODUCT_CODE, TXT_SERVICE_TYPE, TXT_WSDL_TYPE," +
                                " NUM_REPORT_TYPE_CODE, NUM_HTML_TEMPLATE_NO, NUM_HTML_PATTERN_NO, NUM_SERIAL_NO, NUM_LABEL_CODE," +
                                " TXT_LABEL_NAME, TXT_USER_ID, DAT_INSERT_DATE)" +
                                " values ('{1}', '{2}', '{3}', '{4}', '{5}', '{6}', '{7}', '{8}', '{9}'" +
                                ", '{10}', '{11}', to_date('{12}','DD-MM-RRRR'));", table_type, item.NUM_LINE_OF_BUSINESS_CODE, item.NUM_PRODUCT_CODE, item.TXT_SERVICE_TYPE,
                                item.TXT_WSDL_TYPE, item.NUM_REPORT_TYPE_CODE, item.NUM_HTML_TEMPLATE_NO, item.NUM_HTML_PATTERN_NO, item.NUM_SERIAL_NO,
                                item.NUM_LABEL_CODE, item.TXT_LABEL_NAME, item.TXT_USER_ID, item.DAT_INSERT_DATE, target_schema.Trim()));

                            str_builder.Append(System.Environment.NewLine);
                        }

                        str_builder.Append("commit;");

                        break;
                    case "wsdl_report_table_mapping":

                        var wsdl_report_table_mapping_data = (IEnumerable<wsdl_report_table_mapping>)Session["TableData"];

                        str_builder.Append(string.Format("delete from {2}.{0} k where k.num_product_code={1}", table_type.Trim(), product_code.Trim(), target_schema.Trim()));

                        if (report_type_code != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_report_type_code={0}", report_type_code.Trim()));
                        }

                        if (html_template_no != string.Empty)
                        {
                            str_builder.Append(string.Format(" and k.num_html_template_no={0}", html_template_no.Trim()));
                        }

                        str_builder.Append(";");
                        str_builder.Append(System.Environment.NewLine);
                        str_builder.Append("commit;");
                        str_builder.Append(System.Environment.NewLine);

                        foreach (var item in wsdl_report_table_mapping_data)
                        {
                            str_builder.Append(string.Format("insert into {36}.{0} (NUM_LINE_OF_BUSINESS_CODE, NUM_PRODUCT_CODE, NUM_REPORT_TYPE_CODE, NUM_HTML_TEMPLATE_NO," +
                                " TXT_WSDL_TYPE, NUM_SERIAL_NO, NUM_LABEL_CODE, TXT_LABEL_NAME, TXT_TABLE_NAME," +
                                " NUM_COLUMN_SEQUENCE, TXT_IS_COLUMN_VISIBLE, TXT_COLUMN_HEADING, TXT_HEADING_IS_BOLD, TXT_HEADING_ALIGMENT, TXT_HEADER_WIDTH," +
                                " TXT_RISK_COVER, TXT_COLUMN_NAME, TXT_DATA_TYPE, TXT_DATA_FORMAT, TXT_CELL_IS_BOLD, TXT_CELL_ALIGMENT, TXT_DEFAULT_VALUE," +
                                " TXT_WHERE_CLAUSE, TXT_ORDER_BY, TXT_GROUP_FUNCTION, TXT_GROUP_BY, TXT_DISPLAY_TYPE, TXT_WSDL_PROPERTY," +
                                " TXT_HAS_PARENT ,TXT_WSDL_PARENT_PROPERTY, TXT_WSDL_CONDITION_PROPERTY, TXT_WSDL_CONDITION, TXT_REPLACE_VALUE, TXT_USER_ID, DAT_INSERT_DATE)" +
                                " values ('{1}', '{2}', '{3}', '{4}', '{5}', '{6}', '{7}', '{8}', '{9}'" +
                                ", '{10}', '{11}', '{12}', '{13}', '{14}', '{15}', '{16}', '{17}', '{18}', '{19}', '{20}', '{21}', '{22}', '{23}', '{24}', '{25}', '{26}'" +
                                ", '{27}', '{28}', '{29}', '{30}', '{31}', '{32}', '{33}', '{34}', to_date('{35}','DD-MM-RRRR'));", table_type, item.NUM_LINE_OF_BUSINESS_CODE, item.NUM_PRODUCT_CODE, item.NUM_REPORT_TYPE_CODE,
                                item.NUM_HTML_TEMPLATE_NO, item.TXT_WSDL_TYPE, item.NUM_SERIAL_NO, item.NUM_LABEL_CODE, item.TXT_LABEL_NAME,
                                item.TXT_TABLE_NAME, item.NUM_COLUMN_SEQUENCE, item.TXT_IS_COLUMN_VISIBLE, item.TXT_COLUMN_HEADING, item.TXT_HEADING_IS_BOLD,
                                item.TXT_HEADING_ALIGMENT, item.TXT_HEADER_WIDTH, item.TXT_RISK_COVER, item.TXT_COLUMN_NAME, item.TXT_DATA_TYPE, item.TXT_DATA_FORMAT,
                                item.TXT_CELL_IS_BOLD, item.TXT_CELL_ALIGMENT, item.TXT_DEFAULT_VALUE, item.TXT_WHERE_CLAUSE, item.TXT_ORDER_BY, item.TXT_GROUP_FUNCTION,
                                item.TXT_GROUP_BY, item.TXT_DISPLAY_TYPE, item.TXT_WSDL_PROPERTY, item.TXT_HAS_PARENT, item.TXT_WSDL_PARENT_PROPERTY, item.TXT_WSDL_CONDITION_PROPERTY,
                                item.TXT_WSDL_CONDITION, item.TXT_REPLACE_VALUE, item.TXT_USER_ID, item.DAT_INSERT_DATE, target_schema.Trim()));

                            str_builder.Append(System.Environment.NewLine);
                        }

                        str_builder.Append("commit;");

                        break;
                    default:
                        break;
                }

                file_path = output_path + string.Format(@"\{0}_{1}_{2}.sql", sequence_no.Trim(), table_type.Trim(), product_code.Trim());
                Session["FilePath"] = file_path.Trim();
                Session["FileName"] = string.Format(@"{0}_{1}_{2}.sql", sequence_no.Trim(), table_type.Trim(), product_code.Trim());
                str_writer = new StreamWriter(file_path, false);
                str_writer.Write(str_builder);
                str_writer.Close();
                result = "success";
            }
            catch (Exception ex)
            {

                result = "Error: " + ex.Message.ToString().Trim();
            }
            return result;
        }

    }
}
