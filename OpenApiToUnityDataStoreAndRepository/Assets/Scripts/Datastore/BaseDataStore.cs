using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.Linq;

namespace Qitz.DataStoreExtension
{
    public class BaseDataStore<T> : ScriptableObject
    {
        [SerializeField, Header("読み込み元のGoogoleSpreadSheetのurl")]
        string loadingServerUrl;

        [SerializeField]
        List<T> items;
        public List<T> Items => items;

        [ContextMenu("サーバーからデータを読み込む")]
        protected virtual void LoadDataFromServer()
        {

            var ga = new GameObject();
            var referrer = ga.AddComponent<StartCorutinReferrer>();

            referrer.StartCoroutine(JsonFromGoogleSpreadSheet.GetJsonArrayFromGoogleSpreadSheetUrl(loadingServerUrl, (jsonArry) =>
            {
                items = jsonArry.Select(j => JsonUtility.FromJson<T>(j)).ToList();
                DestroyImmediate(ga);
            //Destroy(ga);
        }));

        }
    }
}