using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Qitz.DataStoreExtension;

public class TestDataStore : BaseDataStore<TestVO>
{
    [ContextMenu("サーバーからデータを読み込む")]
    protected override void LoadDataFromServer()
    {
        base.LoadDataFromServer();
    }
}

[System.Serializable]
public class TestVO
{
    [SerializeField]
    int id;
    [SerializeField]
    string 名称;

    public string GetNameByID(int id,string name)
    {
        throw new System.NotImplementedException();
        return 名称;
        //return repositoryLowerName.GetNameByID(id,name);
    }

}
