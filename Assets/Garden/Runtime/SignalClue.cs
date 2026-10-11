using UnityEngine;

namespace Garden
{
    public sealed class SignalClue : MonoBehaviour
    {
        public Loop loop;
        public GameObject[] views = System.Array.Empty<GameObject>();
        public Transform[] arms = System.Array.Empty<Transform>();
        Quaternion[] poses;
        bool present;
        float began;
        int shown = -1;

        void Awake()
        {
            poses = new Quaternion[arms.Length];
            for (int i=0;i<arms.Length;i++) if (arms[i]) poses[i]=arms[i].localRotation;
        }

        void Update()
        {
            bool active = loop && loop.InAngel && !loop.Busy;
            if (active && !present) began=Time.unscaledTime;
            present=active;
            float age=Time.unscaledTime-began;
            int mode=!active?-1:loop.state.hatch?2:loop.state.glove || age>=8 && age<14?1:0;
            if (mode!=shown)
            {
                shown=mode;
                for(int i=0;i<views.Length;i++) if(views[i]) views[i].SetActive(i==mode);
            }
            if (mode==0)
                for(int i=0;i<arms.Length;i++)
                    if(arms[i]) arms[i].localRotation=poses[i]*Quaternion.Euler(0,0,Mathf.Sin(age*2.3f-i*2.1f)*13);
            if (mode==2 && views.Length>2 && views[2]) views[2].transform.localRotation=Quaternion.Euler(0,Mathf.Sin(age*.6f)*25,0);
        }
    }
}
