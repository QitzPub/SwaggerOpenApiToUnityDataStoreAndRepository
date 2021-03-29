using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MiniJSON;
using System;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine.Networking;

public class OpneApiLoader : MonoBehaviour
{
    struct ModelProperty
    {
        public string Type;
        public string PrivateName;
        public string PublicName;
        public ModelProperty(string type, string privateName, string publicName)
        {
            this.Type = type;
            this.PrivateName = privateName;
            this.PublicName = publicName;
        }
    }
    struct ModelInformation
    {
        public string ModelName;
        public List<ModelProperty> Properties;
        public ModelInformation(string modelName, List<ModelProperty> properties)
        {
            this.ModelName = modelName;
            this.Properties = properties;
        }
    }




    const string SAVE_FOLDER_NAME = "変換して作ったDataStoreとかRepository達はここ";
#if UNITY_EDITOR
    string FilePath => Application.dataPath;
#else
string FilePath => AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
#endif

    [SerializeField]
    bool createInteraface = true;
    Action<string> erorrAction;
    Action sccessAction;

    public IEnumerator GetJsonAndCreateModel(string url, bool createInteraface,Action<string> erorrAction,Action sccessAction)
    {
        this.erorrAction = erorrAction;
        this.sccessAction = sccessAction;

        this.createInteraface = createInteraface;
        UnityWebRequest www = UnityWebRequest.Get(url);
        yield return www.SendWebRequest();

        if (www.isNetworkError || www.isHttpError)
        {
            this.erorrAction.Invoke(www.error);
            throw new Exception(www.error);
        }
        else
        {
            try
            {
                CSharpCreateModelsFromOpenApiJson(www.downloadHandler.text);
            }catch(Exception e)
            {
                this.erorrAction.Invoke(e.ToString());
            }            
            this.sccessAction.Invoke();
            Debug.Log("Create Models Success!!");
        }
    }

    void CSharpCreateModelsFromOpenApiJson(string json)
    {
        CreateFolder(FilePath + "/" + SAVE_FOLDER_NAME);
        Dictionary<string, object> dict = Json.Deserialize(json) as Dictionary<string, object>;
        if (!dict.ContainsKey("components"))
        {
            //コンポーネント定義がない場合は生成を行わない
            return;
        }
        var componentsDict = (Dictionary<string, object>)dict["components"];
        var schemasDict = (Dictionary<string, object>)componentsDict["schemas"];
        ProjectInformation projectInformation = GetProjectlInfoFromOpenApiModel(dict);
        var infoDict = (Dictionary<string, object>)dict["info"];
        string titleText = ((string)infoDict["title"]).Replace(" ","").Replace("　", "");

        foreach (var model in schemasDict)
        {
            var modelInfo = GetModelInfoFromOpenApiModel(model);
            CreateCSharpCodeModel(FilePath + "/" + SAVE_FOLDER_NAME, modelInfo, createInteraface);
            CreateDatastore(FilePath + "/" + SAVE_FOLDER_NAME, modelInfo);
            var guid = CreateDatastoreScriptMetaAndGUID(FilePath + "/" + SAVE_FOLDER_NAME, modelInfo);
            CreateDatastoreAsset(FilePath + "/" + SAVE_FOLDER_NAME, modelInfo, guid);

            CreateRepository(FilePath + "/" + SAVE_FOLDER_NAME, schemasDict.ToList().Select(o=> GetModelInfoFromOpenApiModel(o)), projectInformation);
            var repositoryGuid = CreateRepositoryScriptMetaAndGUID(FilePath + "/" + SAVE_FOLDER_NAME, titleText);
            CreateRepositoryAsset(FilePath + "/" + SAVE_FOLDER_NAME, titleText, repositoryGuid);
            if (createInteraface)
            {
                CreateCSharpCodeInterface(FilePath + "/" + SAVE_FOLDER_NAME, modelInfo);
                CreateControllerInterface(FilePath + "/" + SAVE_FOLDER_NAME, projectInformation);
            }
            CreateController(FilePath + "/" + SAVE_FOLDER_NAME, projectInformation);

        }
    }


    void CreateDatastoreAsset(string savePath, ModelInformation modelInfomation,string guid)
    {
        string newSavePath = savePath + "/MasterData";
        CreateFolder(newSavePath);
        string filePath = newSavePath + "/" + modelInfomation.ModelName + "DataStore.asset";
        //ファイルの存在チェック
        if (File.Exists(filePath))
        {
            //存在する場合は抹消
            File.Delete(filePath);
        }
        Encoding enc = System.Text.Encoding.UTF8;
        //Encoding enc = Encoding.GetEncoding("Shift_JIS");
        using (StreamWriter writer = new StreamWriter(filePath, true, enc))
        {
            writer.WriteLine("%YAML 1.1");

            writer.WriteLine("%TAG !u! tag:unity3d.com,2011:");
            writer.WriteLine("--- !u!114 &11400000");
            writer.WriteLine("MonoBehaviour:");
            writer.WriteLine("  m_ObjectHideFlags: 0");
            writer.WriteLine("  m_CorrespondingSourceObject: {fileID: 0}");
            writer.WriteLine("  m_PrefabInstance: {fileID: 0}");
            writer.WriteLine("  m_PrefabAsset: {fileID: 0}");
            writer.WriteLine("  m_GameObject: {fileID: 0}");
            writer.WriteLine("  m_Enabled: 1");
            writer.WriteLine("  m_EditorHideFlags: 0");
            writer.WriteLine("  m_Script: {fileID: 11500000, guid: "+ guid+", type: 3}");
            writer.WriteLine($"  m_Name: {modelInfomation.ModelName}DataStore");
            writer.WriteLine("  m_EditorClassIdentifier: ");

        }
    }

    void CreateController(string savePath, ProjectInformation projectInformation)
    {
        string repositoryLowerName = (projectInformation.TitleName + "Repository").ToLower();
        string newSavePath = savePath + "/Controller";
        CreateFolder(newSavePath);
        string filePath = newSavePath + "/" + projectInformation.TitleName + "Controller.cs";
        //ファイルの存在チェック
        if (File.Exists(filePath))
        {
            //存在する場合は抹消
            File.Delete(filePath);
        }

        Encoding enc = System.Text.Encoding.UTF8;
        //Encoding enc = Encoding.GetEncoding("Shift_JIS");
        using (StreamWriter writer = new StreamWriter(filePath, true, enc))
        {
            writer.WriteLine("using UnityEngine;");
            writer.WriteLine("using System.Collections.Generic;" + Environment.NewLine);
            writer.WriteLine($"public class {projectInformation.TitleName}Controller : MonoBehaviour"+ (createInteraface?$",I{projectInformation.TitleName}Controller" :""));
            writer.WriteLine("{");
            //実装が入る
            writer.WriteLine("    [SerializeField]");
            writer.WriteLine($"    {projectInformation.TitleName}Repository {repositoryLowerName};");

            foreach (var path in projectInformation.Paths)
            {
                foreach (var pathType in path.PathTypeList)
                {
                    string methodName = $"{pathType}{path.Name}";
                    string argString = "(";
                    for (int i = 0; i < path.Arguments.Count; i++)
                    {
                        argString += $"{path.Arguments[i].Type} {path.Arguments[i].Name.ToLower()}" + (path.Arguments.Count - 1 == i ? ")" : ",");
                    }
                    if (path.Arguments.Count == 0) argString = "()";
                    string argOnlyNameString = "(";
                    for (int i = 0; i < path.Arguments.Count; i++)
                    {
                        argOnlyNameString += $"{path.Arguments[i].Name.ToLower()}" + (path.Arguments.Count - 1 == i ? ")" : ",");
                    }
                    if (path.Arguments.Count == 0) argOnlyNameString = "()";

                    string methodReturnValue = path.PathTypeAndReturns.FirstOrDefault(ptr=>ptr.Type.ToLower()== pathType.ToLower()).ReturnValueType;
                    writer.WriteLine($"    public {methodReturnValue} {methodName}{argString}");
                    writer.WriteLine("    {");
                    writer.WriteLine($"        return {repositoryLowerName}.{methodName}{argOnlyNameString};");
                    writer.WriteLine("    }");
                }
            }
            writer.WriteLine("}");
        }
    }

    void CreateControllerInterface(string savePath, ProjectInformation projectInformation)
    {
        string newSavePath = savePath + "/ControllerInterface";
        CreateFolder(newSavePath);
        string filePath = newSavePath + "/" +"I"+ projectInformation.TitleName + "Controller.cs";
        //ファイルの存在チェック
        if (File.Exists(filePath))
        {
            //存在する場合は抹消
            File.Delete(filePath);
        }

        Encoding enc = System.Text.Encoding.UTF8;
        //Encoding enc = Encoding.GetEncoding("Shift_JIS");
        using (StreamWriter writer = new StreamWriter(filePath, true, enc))
        {
            writer.WriteLine("using UnityEngine;");
            writer.WriteLine("using System.Collections.Generic;" + Environment.NewLine);
            writer.WriteLine($"public interface I{projectInformation.TitleName}Controller");
            writer.WriteLine("{");

            foreach (var path in projectInformation.Paths)
            {
                foreach (var pathType in path.PathTypeList)
                {
                    string methodName = $"{pathType}{path.Name}";
                    string argString = "(";
                    for (int i = 0; i < path.Arguments.Count; i++)
                    {
                        argString += $"{path.Arguments[i].Type} {path.Arguments[i].Name.ToLower()}" + (path.Arguments.Count - 1 == i ? ")" : ",");
                    }
                    if (path.Arguments.Count == 0) argString = "()";
                    //string argOnlyNameString = "(";
                    //for (int i = 0; i < path.Arguments.Count; i++)
                    //{
                    //    argOnlyNameString += $"{path.Arguments[i].Name.ToLower()}" + (path.Arguments.Count - 1 == i ? ")" : ",");
                    //}
                    string methodReturnValue = path.PathTypeAndReturns.FirstOrDefault(ptr => ptr.Type.ToLower() == pathType.ToLower()).ReturnValueType;
                    writer.WriteLine($"    {methodReturnValue} {methodName}{argString};");
                }
            }
            writer.WriteLine("}");
        }
    }

    void CreateDatastore(string savePath, ModelInformation modelInfomation)
    {
        string newSavePath = savePath + "/Datastore";
        CreateFolder(newSavePath);
        string filePath = newSavePath + "/" + modelInfomation.ModelName + "DataStore.cs";
        //ファイルの存在チェック
        if (File.Exists(filePath))
        {
            //存在する場合は抹消
            File.Delete(filePath);
        }
        Encoding enc = System.Text.Encoding.UTF8;
        //Encoding enc = Encoding.GetEncoding("Shift_JIS");
        using (StreamWriter writer = new StreamWriter(filePath, true, enc))
        {
            writer.WriteLine("using UnityEngine;");
            writer.WriteLine("using Qitz.DataStoreExtension;");
            writer.WriteLine("using System.Collections.Generic;" + Environment.NewLine);
            writer.WriteLine($"public class {modelInfomation.ModelName}DataStore : BaseDataStore<{modelInfomation.ModelName}>");
            writer.WriteLine("{");
            writer.WriteLine("    [ContextMenu(\"サーバーからデータを読み込む\")]");
            writer.WriteLine("    protected override void LoadDataFromServer()");
            writer.WriteLine("    {");
            writer.WriteLine("        base.LoadDataFromServer();");
            writer.WriteLine("    }");
            writer.WriteLine("}");
        }
    }

    string CreateDatastoreScriptMetaAndGUID(string savePath, ModelInformation modelInfomation)
    {
        string guid = System.Guid.NewGuid().ToString().Replace("-","");
        string newSavePath = savePath + "/Datastore";
        CreateFolder(newSavePath);
        string filePath = newSavePath + "/" + modelInfomation.ModelName + "DataStore.cs.meta";
        //ファイルの存在チェック
        if (File.Exists(filePath))
        {
            //存在する場合は抹消
            File.Delete(filePath);
        }
        Encoding enc = System.Text.Encoding.UTF8;
        //Encoding enc = Encoding.GetEncoding("Shift_JIS");
        using (StreamWriter writer = new StreamWriter(filePath, true, enc))
        {
            writer.WriteLine("fileFormatVersion: 2");
            writer.WriteLine($"guid: {guid}");
            writer.WriteLine("MonoImporter:");
            writer.WriteLine("  externalObjects: {}");
            writer.WriteLine("  serializedVersion: 2");
            writer.WriteLine("  defaultReferences: []");
            writer.WriteLine("  executionOrder: 0");
            writer.WriteLine("  icon: {instanceID: 0}");
            writer.WriteLine("  userData: ");
            writer.WriteLine("  assetBundleName: ");
            writer.WriteLine("  assetBundleVariant: ");
        }

        return guid;
    }

    void CreateRepositoryAsset(string savePath, string repositoryName, string guid)
    {
        string newSavePath = savePath + "/RepositoryAsset";
        CreateFolder(newSavePath);
        string filePath = newSavePath + "/" + repositoryName + "Repository.asset";
        //ファイルの存在チェック
        if (File.Exists(filePath))
        {
            //存在する場合は抹消
            File.Delete(filePath);
        }
        Encoding enc = System.Text.Encoding.UTF8;
        //Encoding enc = Encoding.GetEncoding("Shift_JIS");
        using (StreamWriter writer = new StreamWriter(filePath, true, enc))
        {
            writer.WriteLine("%YAML 1.1");

            writer.WriteLine("%TAG !u! tag:unity3d.com,2011:");
            writer.WriteLine("--- !u!114 &11400000");
            writer.WriteLine("MonoBehaviour:");
            writer.WriteLine("  m_ObjectHideFlags: 0");
            writer.WriteLine("  m_CorrespondingSourceObject: {fileID: 0}");
            writer.WriteLine("  m_PrefabInstance: {fileID: 0}");
            writer.WriteLine("  m_PrefabAsset: {fileID: 0}");
            writer.WriteLine("  m_GameObject: {fileID: 0}");
            writer.WriteLine("  m_Enabled: 1");
            writer.WriteLine("  m_EditorHideFlags: 0");
            writer.WriteLine("  m_Script: {fileID: 11500000, guid: " + guid + ", type: 3}");
            writer.WriteLine($"  m_Name: {repositoryName}Repository");
            writer.WriteLine("  m_EditorClassIdentifier: ");

        }
    }

    void CreateRepository(string savePath, IEnumerable<ModelInformation> modelInfomation, ProjectInformation projectInformation)
    {
        string newSavePath = savePath + "/Repository";
        CreateFolder(newSavePath);
        string filePath = newSavePath + "/" + projectInformation.TitleName + "Repository" + ".cs";
        //ファイルの存在チェック
        if (File.Exists(filePath))
        {
            //存在する場合は抹消
            File.Delete(filePath);
        }
        Encoding enc = System.Text.Encoding.UTF8;
        //Encoding enc = Encoding.GetEncoding("Shift_JIS");
        using (StreamWriter writer = new StreamWriter(filePath, true, enc))
        {
            writer.WriteLine("using UnityEngine;");
            writer.WriteLine("using System.Collections.Generic;" + Environment.NewLine);

            writer.WriteLine("public class " + projectInformation.TitleName + "Repository" +":" + " ScriptableObject");
            writer.WriteLine("{" + Environment.NewLine);

            //DataStoreの追加
            foreach (var m in modelInfomation)
            {
                writer.WriteLine("  " + "[SerializeField]");
                writer.WriteLine("  " + m.ModelName + "DataStore" + " " + m.ModelName.ToLower()+"DataStore" + ";");
                //writer.WriteLine("  " + "public " + m.ModelName + "DataStore" + " " + m.ModelName.ToUpper() + "DataStore" + " => " + m.ModelName.ToLower() + "DataStore" + ";");
                writer.WriteLine(Environment.NewLine);
            }
            //Apiの追加
            foreach (var path in projectInformation.Paths)
            {
                foreach (var pathType in path.PathTypeList)
                {
                    string methodName = $"{pathType}{path.Name}";
                    string argString = "(";
                    for (int i = 0; i < path.Arguments.Count; i++)
                    {
                        argString += $"{path.Arguments[i].Type} {path.Arguments[i].Name.ToLower()}" + (path.Arguments.Count - 1 == i ? ")" : ",");
                    }
                    if (path.Arguments.Count == 0) argString = "()";
                    string methodReturnValue = path.PathTypeAndReturns.FirstOrDefault(ptr => ptr.Type.ToLower() == pathType.ToLower()).ReturnValueType;
                    writer.WriteLine($"    public {methodReturnValue} {methodName}{argString}");
                    writer.WriteLine("    {");
                    writer.WriteLine("        throw new System.NotImplementedException();");
                    writer.WriteLine("    }");
                }
            }

            writer.WriteLine("}");
        }
    }

    string CreateRepositoryScriptMetaAndGUID(string savePath, string repositoryName)
    {
        string guid = System.Guid.NewGuid().ToString().Replace("-", "");
        string newSavePath = savePath + "/Repository";
        CreateFolder(newSavePath);
        string filePath = newSavePath + "/" + repositoryName + "Repository.cs.meta";
        //ファイルの存在チェック
        if (File.Exists(filePath))
        {
            //存在する場合は抹消
            File.Delete(filePath);
        }
        Encoding enc = System.Text.Encoding.UTF8;
        //Encoding enc = Encoding.GetEncoding("Shift_JIS");
        using (StreamWriter writer = new StreamWriter(filePath, true, enc))
        {
            writer.WriteLine("fileFormatVersion: 2");
            writer.WriteLine($"guid: {guid}");
            writer.WriteLine("MonoImporter:");
            writer.WriteLine("  externalObjects: {}");
            writer.WriteLine("  serializedVersion: 2");
            writer.WriteLine("  defaultReferences: []");
            writer.WriteLine("  executionOrder: 0");
            writer.WriteLine("  icon: {instanceID: 0}");
            writer.WriteLine("  userData: ");
            writer.WriteLine("  assetBundleName: ");
            writer.WriteLine("  assetBundleVariant: ");
        }

        return guid;
    }


    void CreateCSharpCodeModel(string savePath, ModelInformation modelInfomation, bool createInteraface)
    {
        string newSavePath = savePath + "/Model";
        CreateFolder(newSavePath);
        string filePath = newSavePath + "/" + modelInfomation.ModelName + ".cs";
        //ファイルの存在チェック
        if (File.Exists(filePath))
        {
            //存在する場合は抹消
            File.Delete(filePath);
        }
        Encoding enc = System.Text.Encoding.UTF8;
        //Encoding enc = Encoding.GetEncoding("Shift_JIS");
        using (StreamWriter writer = new StreamWriter(filePath, true, enc))
        {
            writer.WriteLine("using UnityEngine;");
            writer.WriteLine("using System.Collections.Generic;" + Environment.NewLine);
            writer.WriteLine("[System.Serializable]");
            writer.WriteLine("public class "+ modelInfomation.ModelName + (createInteraface?":I"+ modelInfomation.ModelName : ""));
            writer.WriteLine("{" + Environment.NewLine);
            foreach (var p in modelInfomation.Properties)
            {
                writer.WriteLine("  "+"[SerializeField]");
                writer.WriteLine("  " + p.Type + " " + p.PrivateName+";");
                writer.WriteLine("  " + "public " + p.Type + " " + p.PublicName + " => "+ p.PrivateName+";");
                writer.WriteLine(Environment.NewLine);
            }
            writer.WriteLine("}");
        }
    }

    void CreateCSharpCodeInterface(string savePath, ModelInformation modelInfomation)
    {
        string newSavePath = savePath + "/Interface";
        CreateFolder(newSavePath);
        string filePath = newSavePath + "/I" + modelInfomation.ModelName + ".cs";
        //ファイルの存在チェック
        if (File.Exists(filePath))
        {
            //存在する場合は抹消
            File.Delete(filePath);
        }
        Encoding enc = System.Text.Encoding.UTF8;
        //Encoding enc = Encoding.GetEncoding("Shift_JIS");
        using (StreamWriter writer = new StreamWriter(filePath, true, enc))
        {
            writer.WriteLine("using UnityEngine;");
            writer.WriteLine("using System.Collections.Generic;" + Environment.NewLine);
            writer.WriteLine("public interface I" + modelInfomation.ModelName);
            writer.WriteLine("{" + Environment.NewLine);
            foreach (var p in modelInfomation.Properties)
            {
                writer.WriteLine("  " + p.Type + " " + p.PublicName + " { get; }");
                writer.WriteLine(Environment.NewLine);
            }
            writer.WriteLine("}");
        }
    }

    struct ProjectInformation
    {
        public string TitleName;
        public List<Path> Paths;
        public ProjectInformation(string titleName, List<Path> paths)
        {
            this.TitleName = titleName;
            this.Paths = paths;
        }
    }

    struct Path
    {
        public string Name;
        public List<string> PathTypeList;
        public List<PathTypeAndReturn> PathTypeAndReturns;
        public List<Argument> Arguments;
        public Path(string name, List<string> pathTypeList, List<PathTypeAndReturn> pathTypeAndReturns, List<Argument> arguments)
        {
            this.Name = name;
            this.PathTypeAndReturns = pathTypeAndReturns;
            this.Arguments = arguments;
            this.PathTypeList = pathTypeList;
        }
    }

    struct PathTypeAndReturn
    {
        public string Type;
        public string ReturnValueType;
        public PathTypeAndReturn(string type, string returnValueType)
        {
            this.Type = type;
            this.ReturnValueType = returnValueType;
        }
    }

    struct Argument
    {
        public string Name;
        public string Type;
        public Argument(string name, string type)
        {
            this.Name = name;
            this.Type = type;
        }
    }

    ProjectInformation GetProjectlInfoFromOpenApiModel(Dictionary<string, object> dict)
    {
        var infoDict = (Dictionary<string, object>)dict["info"];
        string titleText = ((string)infoDict["title"]).Replace(" ", "").Replace("　", "");
        List<Path> paths = new List<Path>();
        var pathDict = (Dictionary<string, object>)dict["paths"];
        foreach (var item in pathDict)
        {
            List<Argument> arguments = new List<Argument>();
            List<PathTypeAndReturn> pathTypeAndReturns = new List<PathTypeAndReturn>();
            var pathInfo = (Dictionary<string, object>)item.Value;
            string pathName = item.Key.Split('/').Where(pn=>pn.IndexOf("{")==-1).Select(pn=>pn.Replace(".","")).Where(pn=>pn!="").FirstOrDefault();
            TextInfo ti = CultureInfo.CurrentCulture.TextInfo;
            if(!string.IsNullOrEmpty(pathName))
            {
                pathName = ti.ToTitleCase(pathName);
            }
            
            List<string> argumentNames = item.Key.Split('/').Where(ik=>ik.IndexOf("{") != -1).Select(ik=>ik.Replace("{","").Replace("}","")).ToList();

            List<string> pathTypeList = new List<string>();
            //引数オブジェクトの作成

            

            if (pathInfo.ContainsKey("get")) {
                var responsesInfo = (Dictionary<string, object>)((Dictionary<string, object>)pathInfo["get"])["responses"];
                string returnValue = GetResponseReturnValueFromResponsesInfoDict(responsesInfo);
                pathTypeList.Add("Get");
                arguments = arguments.Concat(GetArgumentsFromPathInfoAndPathType(pathInfo, "Get")).ToList();
                pathTypeAndReturns.Add(new PathTypeAndReturn("Get", returnValue));
            }
            if (pathInfo.ContainsKey("post")) {
                var responsesInfo = (Dictionary<string, object>)((Dictionary<string, object>)pathInfo["post"])["responses"];
                string returnValue = GetResponseReturnValueFromResponsesInfoDict(responsesInfo);
                pathTypeList.Add("Post");
                arguments = arguments.Concat(GetArgumentsFromPathInfoAndPathType(pathInfo, "Post")).ToList();
                pathTypeAndReturns.Add(new PathTypeAndReturn("Post", returnValue));
            }
            if (pathInfo.ContainsKey("delete"))
            {
                var responsesInfo = (Dictionary<string, object>)((Dictionary<string, object>)pathInfo["delete"])["responses"];
                string returnValue = GetResponseReturnValueFromResponsesInfoDict(responsesInfo);
                pathTypeList.Add("Delete");
                arguments = arguments.Concat(GetArgumentsFromPathInfoAndPathType(pathInfo, "Delete")).ToList();
                pathTypeAndReturns.Add(new PathTypeAndReturn("Delete", returnValue));
            }
            if (pathInfo.ContainsKey("put"))
            {
                var responsesInfo = (Dictionary<string, object>)((Dictionary<string, object>)pathInfo["put"])["responses"];
                string returnValue = GetResponseReturnValueFromResponsesInfoDict(responsesInfo);
                pathTypeList.Add("Put");
                arguments = arguments.Concat(GetArgumentsFromPathInfoAndPathType(pathInfo, "Put")).ToList();
                pathTypeAndReturns.Add(new PathTypeAndReturn("Put", returnValue));
            }
            arguments = arguments.Distinct().ToList();
            paths.Add(new Path(pathName, pathTypeList, pathTypeAndReturns, arguments));
        }
        return new ProjectInformation(titleText, paths);
    }

    List<Argument> GetArgumentsFromPathInfoAndPathType(Dictionary<string, object> pathInfo,string pathType)
    {
        List<Argument> arguments = new List<Argument>();
        if (((Dictionary<string, object>)pathInfo[pathType.ToLower()]).ContainsKey("parameters"))
        {
            var parametersObject = ((Dictionary<string, object>)pathInfo[pathType.ToLower()])["parameters"];
            Debug.Log(parametersObject.GetType());
            var parametersList = (List<object>)parametersObject;
            foreach (var param in parametersList)
            {
                string paramName = (string)((Dictionary<string, object>)param)["name"];
                var schema = (Dictionary<string, object>)((Dictionary<string, object>)param)["schema"];
                string type = "";
                if (schema.ContainsKey("$ref"))
                {
                    var objectTypeNameArry = ((string)schema["$ref"]).Split('/');
                    type = objectTypeNameArry[objectTypeNameArry.Length - 1];
                }
                else
                {
                    type = GetModelTypeFromOpenApiType(((string)schema["type"]));
                }
                arguments.Add(new Argument(paramName, type));
            }
        }
        return arguments;
    }

    string GetResponseReturnValueFromResponsesInfoDict(Dictionary<string, object> responsesInfo)
    {
        string returnValue = "";
        string targetResponseCode = responsesInfo.ContainsKey("200") ? "200" : "default";

        //204か何かで200などにある値を返せない場合
        if(!responsesInfo.ContainsKey(targetResponseCode))
        {
            //適当なdiscriptionを返すためstring型で良い
            return "string";
        }

        var content = ((Dictionary<string, object>)responsesInfo[targetResponseCode])["content"];
        var application = ((Dictionary<string, object>)content)["application/json"];
        var schemaDict = (Dictionary<string, object>)((Dictionary<string, object>)application)["schema"];
        if (schemaDict.ContainsKey("$ref"))
        {
            var objectTypeNameArry = ((string)schemaDict["$ref"]).Split('/');
            returnValue = objectTypeNameArry[objectTypeNameArry.Length - 1];
        }
        else if (schemaDict.ContainsKey("type"))
        {
            returnValue = GetModelTypeFromOpenApiType(((string)schemaDict["type"]));
            if(returnValue == "array")
            {
                returnValue = "List<object>";
            }
        }
        else
        {
            returnValue = "void";
        }
        return returnValue;
    }

    ModelInformation GetModelInfoFromOpenApiModel(KeyValuePair<string, object> model)
    {
        List<ModelProperty> properties = new List<ModelProperty>();
        string modelName = model.Key;
        var modelDict = (Dictionary<string, object>)model.Value;
        if(!((Dictionary<string, object>)modelDict).ContainsKey("properties"))
        {
            return new ModelInformation(modelName, properties);
        }

        var modelPropertyDict = (Dictionary<string, object>)modelDict["properties"];
        foreach (var modelProperty in modelPropertyDict)
        {
            var proPertyName = modelProperty.Key;
            string proPertyPrivateName = proPertyName.ToLower();
            var tempName = proPertyName.Replace("_"," ");
            TextInfo ti = CultureInfo.CurrentCulture.TextInfo;
            string proPertyPublicName = ti.ToTitleCase(tempName);
            proPertyPublicName = proPertyPublicName.Replace(" ", "");
            if (proPertyPrivateName == proPertyPublicName)
            {
                proPertyPublicName = "_" + proPertyPublicName;
            }
            var openApiModelPropertyType = "";
            if (((Dictionary<string, object>)modelProperty.Value).ContainsKey("type"))
            {
                openApiModelPropertyType = (string)((Dictionary<string, object>)modelProperty.Value)["type"];
            }
            else
            {
                openApiModelPropertyType = "object";
            }
            string modelPropertyType = GetModelTypeFromOpenApiType(openApiModelPropertyType, modelProperty);
            properties.Add(new ModelProperty(modelPropertyType, proPertyPrivateName, proPertyPublicName));
        }
        return new ModelInformation(modelName, properties);
    }


    string GetModelTypeFromOpenApiType(string openApiModelPropertyType,KeyValuePair<string,object> modelProperty)
    {
        string modelPropertyType = "";
        if (openApiModelPropertyType == "integer")
        {
            modelPropertyType = "int";
        }
        else if (openApiModelPropertyType == "boolean")
        {
            modelPropertyType = "bool";
        }
        else if (openApiModelPropertyType == "object")
        {
            var propertyItems = (Dictionary<string, object>)modelProperty.Value;
            var objectTypeNameArry = ((string)propertyItems["$ref"]).Split('/');
            var objectTypeName = objectTypeNameArry[objectTypeNameArry.Length - 1];

            modelPropertyType = objectTypeName;
        }
        else if (openApiModelPropertyType == "array")
        {
            var propertyItems = (Dictionary<string, object>)(((Dictionary<string, object>)modelProperty.Value)["items"]);
            if (propertyItems.ContainsKey("type"))
            {
                modelPropertyType = "List<" + (string)propertyItems["type"] + ">";
            }
            else
            {
                var objectTypeNameArry = ((string)propertyItems["$ref"]).Split('/');
                var objectTypeName = objectTypeNameArry[objectTypeNameArry.Length - 1];

                modelPropertyType = "List<" + objectTypeName + ">";
            }
        }
        else
        {
            modelPropertyType = openApiModelPropertyType;
        }
        return modelPropertyType;
    }

    string GetModelTypeFromOpenApiType(string openApiModelPropertyType)
    {
        string modelPropertyType = "";
        if (openApiModelPropertyType == "integer")
        {
            modelPropertyType = "int";
        }
        else if (openApiModelPropertyType == "boolean")
        {
            modelPropertyType = "bool";
        }
        else
        {
            modelPropertyType = openApiModelPropertyType;
        }
        return modelPropertyType;
    }

    void CreateFolder(string path)
    {
        if (Directory.Exists(path))
        {
            //Console.WriteLine("フォルダがすでにアルヨ");
        }
        else
        {
            
            DirectoryInfo di = new DirectoryInfo(path);
            di.Create();
        }
    }

}

