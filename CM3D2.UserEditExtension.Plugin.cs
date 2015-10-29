using System;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityInjector.Attributes;

namespace CM3D2.UserEditExtension.Plugin
{
    [PluginFilter("CM3D2x64"), PluginFilter("CM3D2x86"), PluginFilter("CM3D2VRx64")]
    [PluginName("CM3D2 UserEditExtension"), PluginVersion("0.0.0.0")]
    public class UserEditExtension : UnityInjector.PluginBase
    {

        #region Constants

        public const string PluginName = "UserEditExtension";
        public const string Version    = "0.0.0.0";

        private readonly string LogLabel = UserEditExtension.PluginName + " : ";

        private readonly float TimePerInit = 1.00f;

        #endregion



        #region Variables

        private int   sceneLevel;
        private bool  bInitCompleted = false;

        private Maid maid;
        private FieldInfo info_boMaid;
        
        private GameObject   goScrollView;
        private UIPanel      uiScrollPanel;
        private UIScrollView uiScrollView;
        private UITable      uiTable;

        #endregion



        #region MonoBehaviour methods

        public void OnLevelWasLoaded(int level)
        {
            if (level == 12) StartCoroutine( initCoroutine() );
            sceneLevel = level;
        }

        #endregion



        #region Callbacks

        public void OnChangeSlider()
        {
          try{
            string key   = UIProgressBar.current.transform.parent.name.Split(':')[1];
            float  value = UIProgressBar.current.value;

            FindChild(UIProgressBar.current.transform.parent.gameObject, "Number").GetComponent<UILabel>().text = (value * 100).ToString("F0");
            updateManShapeKey(key, value);

         } catch(Exception ex) { Debug.Log(LogLabel +"OnChangeSlider() "+ ex); return; }
        }

        #endregion



        #region Private methods

        private IEnumerator initCoroutine()
        {
            while ( !(bInitCompleted = initialize()) ) yield return new WaitForSeconds(TimePerInit);
            Debug.Log(LogLabel +"Initialization complete.");
        }

        private bool initialize()
        {
          try{
            maid = GameMain.Instance.CharacterMgr.GetMan(0);
            if (maid == null) return false;

            info_boMaid = GetFieldInfo<TBody>("boMaid");
            if (info_boMaid == null) return false;

            GameObject goEditPanel = GameObject.Find("UserEditPanel");
            if (goEditPanel == null) return false;


            // UserEditPanelをスクロールビュー化
            UIPanel uiEditPanel = goEditPanel.GetComponent<UIPanel>();
            GameObject goEditPanelBG = goEditPanel.transform.Find("BG").gameObject;
            UISprite uiBGSprite = goEditPanelBG.GetComponent<UISprite>();

            goScrollView = new GameObject("ScrollView");
            SetChild(goEditPanel, goScrollView);
            goScrollView.transform.localPosition = goEditPanelBG.transform.localPosition;

            uiScrollPanel = goScrollView.AddComponent<UIPanel>();
            uiScrollPanel.depth        = uiBGSprite.depth + 1;
            uiScrollPanel.sortingOrder = uiEditPanel.sortingOrder + 1;
            uiScrollPanel.clipping     = UIDrawCall.Clipping.SoftClip;
            uiScrollPanel.SetRect(0f, 0f, uiBGSprite.width - 30, uiBGSprite.height - 120);

            uiScrollView = goScrollView.AddComponent<UIScrollView>();
            uiScrollView.contentPivot = UIWidget.Pivot.Center;
            uiScrollView.movement = UIScrollView.Movement.Vertical;
            goEditPanelBG.AddComponent<UIDragScrollView>().scrollView = uiScrollView;
            goEditPanelBG.AddComponent<BoxCollider>();
            NGUITools.UpdateWidgetCollider(goEditPanelBG);
            
            uiTable = NGUITools.AddChild<UITable>(goScrollView);
            uiTable.pivot           = UIWidget.Pivot.Center;
            uiTable.columns         = 1;
            uiTable.padding         = new Vector2(25f, 20f);
            uiTable.hideInactive    = true;
            uiTable.keepWithinPanel = true;
            uiTable.sorting         = UITable.Sorting.Custom;
            uiTable.onCustomSort    = (Comparison<Transform>)this.sortTable;
            GameObject goTable = uiTable.gameObject;
            uiScrollView.centerOnChild = goTable.AddComponent<UICenterOnChild>();

            GameObject goName    = FindChild(goEditPanel, "Name");
            GameObject goHead    = FindChild(goEditPanel, "Head");
            GameObject goAbdomen = FindChild(goEditPanel, "Abdomen");
            GameObject goColor   = FindChild(goEditPanel, "Color");
 
            SetChild(goTable, goName);
            SetChild(goTable, goHead);
            SetChild(goTable, goAbdomen);
            SetChild(goTable, goColor);

            List<Component> list = new List<Component>();
            list.AddRange( goName.GetComponentsInChildren<Component>() );
            list.AddRange( goHead.GetComponentsInChildren<Component>() );
            list.AddRange( goAbdomen.GetComponentsInChildren<Component>() );
            list.AddRange( goColor.GetComponentsInChildren<Component>() );
            foreach (UIWidget uw in list.Where(c => c is UIWidget)) uw.ParentHasChanged();

            GameObject goAbdomenSlider = FindChild(goAbdomen, "Slider");
            FindChild(goAbdomen, "Value" ).transform.localPosition -= goAbdomenSlider.transform.localPosition;
            goAbdomenSlider.transform.localPosition = Vector3.zero;

            UILabel uiLabelAbdomanTitle = FindChild(goAbdomen, "Title").GetComponent<UILabel>();
            uiLabelAbdomanTitle.width     = 250;
            uiLabelAbdomanTitle.text      = "体 (karadal)";
            uiLabelAbdomanTitle.alignment = NGUIText.Alignment.Left;
            uiLabelAbdomanTitle.transform.localPosition = new Vector3(-110f, uiLabelAbdomanTitle.transform.localPosition.y, uiLabelAbdomanTitle.transform.localPosition.z);
            FindChild(goAbdomen, "Thumb").GetComponent<UIDragScrollView>().enabled = false;
            //WriteChildrenComponent(goAbdomen);

            GameObject goGategoryTitle = FindChild(goColor, "CategoryTitle");
            FindChild(goColor, "R"      ).transform.localPosition -= goGategoryTitle.transform.localPosition;
            FindChild(goColor, "G"      ).transform.localPosition -= goGategoryTitle.transform.localPosition;
            FindChild(goColor, "B"      ).transform.localPosition -= goGategoryTitle.transform.localPosition;
            FindChild(goColor, "ColorBG").transform.localPosition -= goGategoryTitle.transform.localPosition;
            goGategoryTitle.transform.localPosition = Vector3.zero;
            //WriteChildrenComponent(goColor);


            // karadal以外のシェイプキーのスライダー追加
            foreach (string key in maid.body0.goSlot[0].morph.hash.Keys) 
            {
                if (key == "karadal") continue;
                
                GameObject goAdditional = SetCloneChild(goTable, goAbdomen, "Slider:"+ key);
                FindChild(goAdditional, "Title").GetComponent<UILabel>().text = key;
                EventDelegate.Set(goAdditional.GetComponentsInChildren<UISlider>()[0].onChange, this.OnChangeSlider);

                float x;
                FindChild(goAdditional, "Slider").GetComponent<UISlider>().value = Single.TryParse(Preferences["ShaKey"][key].Value, out x) ? x : 0;
            }
            
            maid.body0.SetChinkoVisible(true);


            // OKボタンに終了処理差込
            GameObject goOk = FindChild(goEditPanel, "Ok");
            UIEventListener.Get(goOk).onClick += (UIEventListener.VoidDelegate)this.finalize;
            
            uiTable.Reposition();

          } catch(Exception ex) { Debug.Log(LogLabel +"initialize() "+ ex); return false; }

            return true;
        }

        private void finalize(GameObject go)
        {
          try {
            foreach (string key in maid.body0.goSlot[0].morph.hash.Keys) 
            {
                if (key == "karadal") continue;

                GameObject goAdditional = FindChild(uiTable.gameObject, "Slider:"+ key);
                Preferences["ShaKey"][key].Value = (FindChild(goAdditional, "Slider").GetComponent<UISlider>().value * 100).ToString("F0");
            }
            SaveConfig();
          } catch(Exception ex) { Debug.LogError(LogLabel +"finalize() "+ ex); }
        }
        
        //----

        private void updateManShapeKey(string key, float value)
        {
          try {
            info_boMaid.SetValue(maid.body0, true);
            maid.body0.VertexMorph_FromProcItem(key, value);
            info_boMaid.SetValue(maid.body0, false);
          } catch(Exception ex) { Debug.LogError(LogLabel +"updateManShapeKey() "+ ex); }
        }

        private int sortTable(Transform t1, Transform t2)
        {
          try {

            Dictionary<string, int> order = new Dictionary<string, int>()
                { {"Name", 0}, {"Head", 1}, {"Abdomen", 2}, {"Slider", 3},{"Color", 4} };

            string name1 = (t1.name.Split(':') != null) ? t1.name.Split(':')[0] : t1.name;
            string name2 = (t2.name.Split(':') != null) ? t2.name.Split(':')[0] : t2.name;

            if (name1 == "Slider" && name2 == "Slider") return t1.name.Split(':')[1].CompareTo(t2.name.Split(':')[1]);
            else return order[name1] - order[name2];

          } catch(Exception ex) { Debug.Log(LogLabel +"sortTable() "+ ex); return 0; }
        }

        #endregion



        #region Utility methods

        internal static GameObject FindChild(GameObject go, string s)
        {
            if (go == null) return null;
            GameObject target = null;
            
            foreach (Transform tc in go.transform)
            {
                if (tc.gameObject.name == s) return tc.gameObject;
                target = FindChild(tc.gameObject, s);
                if (target) return target;
            } 
            
            return null;
        }

        internal static void SetChild(GameObject parent, GameObject child)
        {
            child.layer                   = parent.layer;
            child.transform.parent        = parent.transform;
            child.transform.localPosition = Vector3.zero;
            child.transform.localScale    = Vector3.one;
            child.transform.rotation      = Quaternion.identity;
        }

        internal static GameObject SetCloneChild(GameObject parent, UnityEngine.Object orignal, string name)
        {
            GameObject clone = UnityEngine.Object.Instantiate(orignal) as GameObject;
            if (!clone) return null;

            clone.name = name;
            SetChild(parent, clone);

            return clone;
        }

        internal static FieldInfo GetFieldInfo<T>(string name)
        {
            BindingFlags bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

            return typeof(T).GetField(name, bf);
        }

        internal static TResult GetFieldValue<T, TResult>(T inst, string name)
        {
            if (inst == null)  return default(TResult);

            FieldInfo field = GetFieldInfo<T>(name);
            if (field == null) return default(TResult);

            return (TResult)field.GetValue(inst);
        }

        internal static void WriteChildrenComponent(GameObject go)
        {
            Debug.LogWarning(go.transform.localPosition);
            WriteComponent(go);
            
            foreach (Transform tc in go.transform)
            {
                WriteChildrenComponent(tc.gameObject);
            }
        }

        internal static void WriteComponent(GameObject go)
        {
            Component[] compos = go.GetComponents<Component>();
            foreach(Component c in compos){ Debug.Log(go.name +":"+ c.GetType().Name); }
        }

        #endregion

    }
}

