using System.IO;
using System.Text;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum CartState
{
    idle = 0,
    idle_open = 1,
    up = 2,
    up_open = 3,
    down = 4,
    down_open = 5,
}

public class Cart
{
    public int pos = 1;
    public CartState state = CartState.idle;
    public SortedSet<int> stopLevels = new SortedSet<int>();
    public SortedSet<int> upLevels = new SortedSet<int>();
    public SortedSet<int> downLevels = new SortedSet<int>();
    
    public void AddLevel(int level)
    {
        if ((state == CartState.up || state == CartState.up_open) && pos < level ||
            (state == CartState.down || state == CartState.down_open) && pos > level)
            stopLevels.Add(level);
    }

    public void AddLevel(int level, bool up)
    {
        bool ret = up ? upLevels.Add(level) : downLevels.Add(level);
    }

    public int GetResponseTime(int level, bool up)
    {
        switch (state)
        {
            case CartState.idle:
            case CartState.idle_open:
                return Mathf.Abs(pos - level);
            case CartState.up:
            case CartState.up_open:
                {
                    if (up)
                    {
                        if (pos < level)
                        {
                            return Mathf.Abs(pos - level);
                        }
                        else
                        {
                            int highest = Mathf.Max(upLevels.Count == 0 ? pos : upLevels.Max, downLevels.Count == 0 ? pos : downLevels.Max);
                            highest = Mathf.Max(highest, stopLevels.Count == 0 ? pos : stopLevels.Max);
                            int lowest = Mathf.Min(upLevels.Count == 0 ? pos : upLevels.Min, downLevels.Count == 0 ? pos : upLevels.Min);
                            return Mathf.Abs(pos - highest) + Mathf.Abs(highest - lowest) + Mathf.Abs(lowest - level);
                        }
                    }
                    else
                    {
                        int highest = Mathf.Max(upLevels.Count == 0 ? pos : upLevels.Max, downLevels.Count == 0 ? pos : downLevels.Max);
                        highest = Mathf.Max(highest, stopLevels.Count == 0 ? pos : stopLevels.Max);
                        return Mathf.Abs(pos - highest) + Mathf.Abs(highest - level);
                    }
                }
            case CartState.down:
            case CartState.down_open:
                {
                    if (!up)
                    {
                        if (pos > level)
                        {
                            return Mathf.Abs(pos - level);
                        }
                        else
                        {
                            int highest = Mathf.Max(upLevels.Count == 0 ? pos : upLevels.Max, downLevels.Count == 0 ? pos : downLevels.Max);
                            int lowest = Mathf.Min(upLevels.Count == 0 ? pos : upLevels.Min, downLevels.Count == 0 ? pos : upLevels.Min);
                            lowest = Mathf.Min(lowest, stopLevels.Count == 0 ? pos : stopLevels.Min);
                            return Mathf.Abs(pos - lowest) + Mathf.Abs(lowest - highest) + Mathf.Abs(highest - level);
                        }
                    }
                    else
                    {
                        int lowest = Mathf.Min(upLevels.Count == 0 ? pos : upLevels.Min, downLevels.Count == 0 ? pos : upLevels.Min);
                        lowest = Mathf.Min(lowest, stopLevels.Count == 0 ? pos : stopLevels.Min);
                        return Mathf.Abs(pos - lowest) + Mathf.Abs(lowest - level);
                    }
                }
            default:
                return int.MaxValue;
        }
    }
}

public class Elevator
{
    public const int cartNum = 4;
    public const int levelNum = 40;

    public Mutex mtx = new Mutex();
    public List<Cart> carts = new List<Cart>();
    public Queue<int> upQueue = new Queue<int>(), downQueue = new Queue<int>();
    public Dictionary<int, int> upDic = new Dictionary<int, int>(), downDic = new Dictionary<int, int>();

    public Elevator()
    {
        for (int i = 0; i < cartNum; ++i)
        {
            carts.Add(new Cart());
        }
    }
}

public class Record
{
    public int timestamp;
    public HashSet<int> upData = new HashSet<int>();
    public HashSet<int> downData = new HashSet<int>();
    public List<Cart> carts = new List<Cart>();
}

public class Main : MonoBehaviour
{
    public bool isReplay = false;

    private int levelNum;
    private int cartNum;

    private List<Record> data = new List<Record>();

    private Elevator sys = new Elevator();

    private GameObject buttonPrefab;
    private GameObject cartPrefab;
    private GameObject levelPrefab;

    private Text timeText;
    private Transform levelPanel;
    private Transform cartPanel;
    private List<Transform> levels = new List<Transform>();
    private List<Transform> cartsUI = new List<Transform>();
    private List<List<Transform>> buttons = new List<List<Transform>>();

    private float ctrlInternal = 0.1f;
    private float idleInternal = 0.2f;
    private float moveInternal = 0.5f;
    private float stopInternal = 5.0f;

    private IEnumerator UpControl()
    {
        while (true)
        {
            if(sys.upQueue.Count == 0)
            {
                yield return new WaitForSeconds(ctrlInternal);
            }
            else
            {
                sys.mtx.WaitOne();

                List<KeyValuePair<int, int>> prior = new List<KeyValuePair<int, int>>();
                for(int i = 0;i < Elevator.cartNum; ++i)
                {
                    prior.Add(new KeyValuePair<int, int>(i, sys.carts[i].GetResponseTime(sys.upQueue.Peek(), true)));
                }
                prior.Sort((KeyValuePair<int, int> a, KeyValuePair<int, int> b) => {
                    return a.Value.CompareTo(b.Value);
                });

                sys.upDic[sys.upQueue.Peek()] = prior[0].Key;
                sys.carts[prior[0].Key].AddLevel(sys.upQueue.Dequeue(), true);

                sys.mtx.ReleaseMutex();
            }
        }
    }

    private IEnumerator DownControl()
    {
        while (true)
        {
            if (sys.downQueue.Count == 0)
            {
                yield return new WaitForSeconds(ctrlInternal);
            }
            else
            {
                sys.mtx.WaitOne();

                List<KeyValuePair<int, int>> prior = new List<KeyValuePair<int, int>>();
                for (int i = 0; i < Elevator.cartNum; ++i)
                {
                    prior.Add(new KeyValuePair<int, int>(i, sys.carts[i].GetResponseTime(sys.downQueue.Peek(), true)));
                }
                prior.Sort((KeyValuePair<int, int> a, KeyValuePair<int, int> b) => {
                    return a.Value.CompareTo(b.Value);
                });

                sys.downDic[sys.downQueue.Peek()] = prior[0].Key;
                sys.carts[prior[0].Key].AddLevel(sys.downQueue.Dequeue(), false);

                sys.mtx.ReleaseMutex();
            }
        }
    }

    private IEnumerator CartHandle(int index)
    {
        Cart cart = sys.carts[index];

        while (true)
        {
            switch (cart.state)
            {
                case CartState.idle:
                    yield return new WaitForSeconds(idleInternal);
                    break;
                case CartState.up:
                case CartState.down:
                    yield return new WaitForSeconds(moveInternal);
                    break;
                case CartState.up_open:
                case CartState.down_open:
                case CartState.idle_open:
                    yield return new WaitForSeconds(stopInternal);
                    break;
            }

            switch (cart.state)
            {
                case CartState.idle:
                    {
                        if (cart.upLevels.Count != 0)
                        {
                            int begin = cart.upLevels.Min;
                            if (begin < cart.pos)
                            {
                                cart.state = CartState.down;
                            }
                            else if (begin == cart.pos)
                            {
                                cart.upLevels.Remove(cart.pos);
                                cart.state = CartState.up_open;
                            }
                            else
                            {
                                cart.state = CartState.up;
                            }
                        }
                        else if (cart.downLevels.Count != 0)
                        {
                            int end = cart.downLevels.Max;
                            if (end > cart.pos)
                            {
                                cart.state = CartState.up;
                            }
                            else if (end == cart.pos)
                            {
                                cart.downLevels.Remove(cart.pos);
                                cart.state = CartState.down_open;
                            }
                            else
                            {
                                cart.state = CartState.down;
                            }
                        }
                        else
                        {
                            cart.state = CartState.idle;
                        }
                        break;
                    }
                case CartState.up:
                    {
                        ++cart.pos;

                        if (cart.upLevels.Contains(cart.pos))
                        {
                            cart.upLevels.Remove(cart.pos);
                            cart.state = CartState.up_open;

                            if (cart.stopLevels.Contains(cart.pos))
                                cart.stopLevels.Remove(cart.pos);
                        }
                        else if (cart.stopLevels.Contains(cart.pos))
                        {
                            cart.stopLevels.Remove(cart.pos);

                            if (cart.stopLevels.Count == 0 && cart.upLevels.Count == 0 && cart.downLevels.Count == 0)
                            {
                                cart.state = CartState.idle_open;
                            }
                            else if (cart.stopLevels.Count == 0)
                            {
                                int highest = Mathf.Max(cart.upLevels.Count == 0 ? cart.pos : cart.upLevels.Max, cart.downLevels.Count == 0 ? cart.pos : cart.downLevels.Max);
                                if (highest > cart.pos)
                                {
                                    cart.state = CartState.up_open;
                                }
                                else if (highest == cart.pos)
                                {
                                    if (cart.downLevels.Contains(cart.pos))
                                    {
                                        cart.downLevels.Remove(cart.pos);
                                        cart.state = CartState.down_open;
                                    }
                                    else
                                    {
                                        cart.state = CartState.down_open;
                                    }
                                }
                            }
                            else
                            {
                                cart.state = CartState.up_open;
                            }
                        }
                        else if (cart.downLevels.Contains(cart.pos)
                            && cart.pos == cart.downLevels.Max
                            && cart.stopLevels.Count == 0
                            && (cart.upLevels.Count == 0 || cart.upLevels.Max < cart.pos))
                        {
                            cart.downLevels.Remove(cart.pos);
                            cart.state = CartState.down_open;
                        }
                        else
                        {
                            cart.state = CartState.up;
                        }
                        break;
                    }
                case CartState.down:
                    {
                        --cart.pos;

                        if (cart.downLevels.Contains(cart.pos))
                        {
                            cart.downLevels.Remove(cart.pos);
                            cart.state = CartState.down_open;

                            if (cart.stopLevels.Contains(cart.pos))
                                cart.stopLevels.Remove(cart.pos);
                        }
                        else if (cart.stopLevels.Contains(cart.pos))
                        {
                            cart.stopLevels.Remove(cart.pos);

                            if (cart.stopLevels.Count == 0 && cart.upLevels.Count == 0 && cart.downLevels.Count == 0)
                            {
                                cart.state = CartState.idle_open;
                            }
                            else if (cart.stopLevels.Count == 0)
                            {
                                int lowest = Mathf.Max(cart.upLevels.Count == 0 ? cart.pos : cart.upLevels.Min, cart.downLevels.Count == 0 ? cart.pos : cart.downLevels.Min);
                                if (lowest < cart.pos)
                                {
                                    cart.state = CartState.down_open;
                                }
                                else if (lowest >= cart.pos)
                                {
                                    if (cart.upLevels.Contains(cart.pos))
                                    {
                                        cart.upLevels.Remove(cart.pos);
                                        cart.state = CartState.up_open;
                                    }
                                    else
                                    {
                                        cart.state = CartState.up_open;
                                    }
                                }
                            }
                            else
                            {
                                cart.state = CartState.down_open;
                            }
                        }
                        else if (cart.upLevels.Contains(cart.pos)
                            && cart.pos == cart.upLevels.Min
                            && cart.stopLevels.Count == 0
                            && (cart.downLevels.Count == 0 || cart.downLevels.Min > cart.pos))
                        {
                            cart.upLevels.Remove(cart.pos);
                            cart.state = CartState.up_open;
                        }
                        else
                        {
                            cart.state = CartState.down;
                        }
                        break;
                    }
                case CartState.up_open:
                    {
                        if (cart.stopLevels.Count == 0 && cart.upLevels.Count == 0 && cart.downLevels.Count == 0)
                        {
                            cart.state = CartState.idle;
                        }
                        else if (cart.stopLevels.Count == 0)
                        {
                            int highest = Mathf.Max(cart.upLevels.Count == 0 ? cart.pos : cart.upLevels.Max, cart.downLevels.Count == 0 ? cart.pos : cart.downLevels.Max);
                            if (highest > cart.pos)
                            {
                                cart.state = CartState.up;
                            }
                            else if (highest == cart.pos)
                            {
                                if (cart.downLevels.Contains(cart.pos))
                                {
                                    cart.downLevels.Remove(cart.pos);
                                    cart.state = CartState.down;
                                }
                                else
                                {
                                    cart.state = CartState.down;
                                }
                            }
                        }
                        else
                        {
                            cart.state = CartState.up;
                        }
                        sys.upDic.Remove(cart.pos);
                        break;
                    }
                case CartState.down_open:
                    {
                        if (cart.stopLevels.Count == 0 && cart.upLevels.Count == 0 && cart.downLevels.Count == 0)
                        {
                            cart.state = CartState.idle;
                        }
                        else if (cart.stopLevels.Count == 0)
                        {
                            int lowest = Mathf.Min(cart.upLevels.Count == 0 ? cart.pos : cart.upLevels.Min, cart.downLevels.Count == 0 ? cart.pos : cart.downLevels.Min);
                            if (lowest < cart.pos)
                            {
                                cart.state = CartState.down;
                            }
                            else if (lowest >= cart.pos)
                            {
                                if (cart.upLevels.Contains(cart.pos))
                                {
                                    cart.upLevels.Remove(cart.pos);
                                    cart.state = CartState.up;
                                }
                                else
                                {
                                    cart.state = CartState.up;
                                }
                            }
                        }
                        else
                        {
                            cart.state = CartState.down;
                        }
                        sys.downDic.Remove(cart.pos);
                        break;
                    }
                case CartState.idle_open:
                    {
                        cart.state = CartState.idle;
                        break;
                    }
            }
        }
    }

    private void Awake()
    {
        buttonPrefab = Resources.Load<GameObject>("Button");
        cartPrefab = Resources.Load<GameObject>("Cart");
        levelPrefab = Resources.Load<GameObject>("Level");

        timeText = GameObject.Find("Canvas/Panel/Out/Time").GetComponentInChildren<Text>();
        levelPanel = GameObject.Find("Canvas/Panel/Out/Scroll View/Viewport/Content").transform;
        cartPanel = GameObject.Find("Canvas/Panel/In/Scroll View/Viewport/Content").transform;

        if (isReplay)
            ReplayInit();
        else
            PlayInit();
    }

    private void PlayInit()
    {
        for (int i = 0; i < Elevator.levelNum; ++i)
        {
            int tmp = i;
            GameObject l = GameObject.Instantiate(levelPrefab, levelPanel);
            l.GetComponentInChildren<Text>().text = (i + 1).ToString();
            l.transform.Find("Up").GetComponent<Button>().onClick.RemoveAllListeners();
            l.transform.Find("Up").GetComponent<Button>().onClick.AddListener(() =>
            {
                if (!sys.upDic.ContainsKey(tmp + 1))
                {
                    sys.upDic.Add(tmp + 1, -1);
                    sys.upQueue.Enqueue(tmp + 1);
                }
            });
            l.transform.Find("Down").GetComponent<Button>().onClick.RemoveAllListeners();
            l.transform.Find("Down").GetComponent<Button>().onClick.AddListener(() =>
            {
                if (!sys.downDic.ContainsKey(tmp + 1))
                {
                    sys.downDic.Add(tmp + 1, -1);
                    sys.downQueue.Enqueue(tmp + 1);
                }
            });
            if (i == 0) l.transform.Find("Down").gameObject.SetActive(false);
            if (i == Elevator.levelNum - 1) l.transform.Find("Up").gameObject.SetActive(false);
            levels.Add(l.transform);
        }

        for (int i = 0; i < Elevator.cartNum; ++i)
        {
            int tmp1 = i;

            GameObject c = GameObject.Instantiate(cartPrefab, cartPanel);

            c.transform.Find("Name").GetComponentInChildren<Text>().text = "Cart " + (i + 1).ToString();

            List<Transform> listB = new List<Transform>();

            for (int j = 0; j < Elevator.levelNum; ++j)
            {
                int tmp2 = j;

                GameObject b = GameObject.Instantiate(buttonPrefab, c.transform.Find("Button"));

                b.GetComponentInChildren<Text>().text = (j + 1).ToString();
                b.GetComponent<Button>().onClick.RemoveAllListeners();
                b.GetComponent<Button>().onClick.AddListener(() =>
                {
                    sys.carts[tmp1].AddLevel(tmp2 + 1);
                });

                listB.Add(b.transform);
            }

            cartsUI.Add(c.transform);
            buttons.Add(listB);
        }

        StartCoroutine(UpControl());
        StartCoroutine(DownControl());
        for (int i = 0; i < Elevator.cartNum; ++i)
            StartCoroutine(CartHandle(i));
    }

    private void ReplayInit()
    {
        FileStream fs = new FileStream(Application.dataPath + "/data.txt", FileMode.Open);
        byte[] bytes = new byte[100 * 1024];
        fs.Read(bytes, 0, bytes.Length);
        string s = Encoding.ASCII.GetString(bytes, 0, bytes.Length);
        string[] records = s.Split('\n');
        for (int n = 1; n < records.Length - 1; ++n)
        {
            string str = records[n];
            Json json = new Json(str);
            Record sta = new Record();
            sta.timestamp = json.GetObject()["timestamp"].GetInt();
            foreach (Json i in json.GetObject()["upQueue"].GetList())
                sta.upData.Add(i.GetInt());
            foreach (Json i in json.GetObject()["downQueue"].GetList())
                sta.downData.Add(i.GetInt());
            foreach (Json i in json.GetObject()["carts"].GetList())
            {
                Cart cart = new Cart();
                cart.pos = i.GetObject()["pos"].GetInt();
                cart.state = (CartState)i.GetObject()["state"].GetInt();
                foreach (Json j in i.GetObject()["stopLevels"].GetList())
                    cart.stopLevels.Add(j.GetInt());
                sta.carts.Add(cart);
            }
            data.Add(sta);
        }

        Json config = new Json(records[0]);
        levelNum = config.GetObject()["levels"].GetInt();
        cartNum = config.GetObject()["carts"].GetInt();

        for (int i = 0; i < levelNum; ++i)
        {
            GameObject l = GameObject.Instantiate(levelPrefab, levelPanel);
            l.GetComponentInChildren<Text>().text = (i + 1).ToString();
            l.transform.Find("Up").GetComponent<Button>().onClick.RemoveAllListeners();
            l.transform.Find("Down").GetComponent<Button>().onClick.RemoveAllListeners();
            if (i == 0) l.transform.Find("Down").gameObject.SetActive(false);
            if (i == levelNum - 1) l.transform.Find("Up").gameObject.SetActive(false);
            levels.Add(l.transform);
        }

        for (int i = 0; i < cartNum; ++i)
        {
            GameObject c = GameObject.Instantiate(cartPrefab, cartPanel);

            c.transform.Find("Name").GetComponentInChildren<Text>().text = "Cart " + (i + 1).ToString();

            List<Transform> listB = new List<Transform>();

            for (int j = 0; j < levelNum; ++j)
            {
                GameObject b = GameObject.Instantiate(buttonPrefab, c.transform.Find("Button"));

                b.GetComponentInChildren<Text>().text = (j + 1).ToString();
                b.GetComponent<Button>().onClick.RemoveAllListeners();

                listB.Add(b.transform);
            }

            cartsUI.Add(c.transform);
            buttons.Add(listB);
        }

        fs.Close();
    }

    private void Update()
    {
        if (isReplay)
            ReplayUpdate();
        else
            PlayUpdate();
    }

    private void PlayUpdate()
    {
        float t = Time.realtimeSinceStartup;

        timeText.text = "Time " + ((int)t).ToString() + " sec";

        UpdateUI();
    }

    private int recordIndex = 0;
    private void ReplayUpdate()
    {
        float t = Time.realtimeSinceStartup;

        timeText.text = "Time " + ((int)t).ToString() + " sec";

        if (recordIndex < data.Count && data[recordIndex].timestamp <= t * 1000)
            UpdateUI(recordIndex++);
    }

    private void UpdateUI()
    {
        List<bool> upL = new List<bool>();
        List<bool> downL = new List<bool>();

        for (int i = 0; i < Elevator.levelNum; ++i)
        {
            upL.Add(false);
            downL.Add(false);
        }

        foreach(Cart cart in sys.carts)
        {
            foreach(int i in cart.upLevels)
            {
                upL[i - 1] = true;
            }

            foreach(int i in cart.downLevels)
            {
                downL[i - 1] = true;
            }
        }

        for (int i = 0; i < Elevator.levelNum; ++i)
        {
            Transform p = levels[i];

            if (upL[i])
                p.Find("Up").GetComponent<Image>().color = Color.yellow;
            else
                p.Find("Up").GetComponent<Image>().color = Color.gray;

            if (downL[i])
                p.Find("Down").GetComponent<Image>().color = Color.yellow;
            else
                p.Find("Down").GetComponent<Image>().color = Color.gray;
        }

        for (int i = 0; i < Elevator.cartNum; ++i)
        {
            Transform c = cartsUI[i];

            Text txt = c.Find("Status/Text").GetComponent<Text>();
            Image img = c.Find("Status").GetComponent<Image>();
            GameObject up = c.Find("Status/Up").gameObject;
            GameObject down = c.Find("Status/Down").gameObject;

            txt.text = sys.carts[i].pos.ToString();

            switch (sys.carts[i].state)
            {
                case CartState.idle:
                    {
                        img.color = Color.red;
                        up.SetActive(false);
                        down.SetActive(false);
                        break;
                    }
                case CartState.idle_open:
                    {
                        img.color = Color.green;
                        up.SetActive(false);
                        down.SetActive(false);
                        break;
                    }
                case CartState.up:
                    {
                        img.color = Color.red;
                        up.SetActive(true);
                        down.SetActive(false);
                        break;
                    }
                case CartState.up_open:
                    {
                        img.color = Color.green;
                        up.SetActive(true);
                        down.SetActive(false);
                        break;
                    }
                case CartState.down:
                    {
                        img.color = Color.red;
                        up.SetActive(false);
                        down.SetActive(true);
                        break;
                    }
                case CartState.down_open:
                    {
                        img.color = Color.green;
                        up.SetActive(false);
                        down.SetActive(true);
                        break;
                    }
            }

            for (int j = 0; j < Elevator.levelNum; ++j)
            {
                if (sys.carts[i].stopLevels.Contains(j + 1))
                    buttons[i][j].GetComponent<Image>().color = Color.green;
                else
                    buttons[i][j].GetComponent<Image>().color = Color.white;
            }
        }
    }

    private void UpdateUI(int index)
    {
        Record sta = data[index];

        for (int i = 0; i < levelNum; ++i)
        {
            Transform p = levels[i];

            if (sta.upData.Contains(i + 1))
                p.Find("Up").GetComponent<Image>().color = Color.yellow;
            else
                p.Find("Up").GetComponent<Image>().color = Color.gray;

            if (sta.downData.Contains(i + 1))
                p.Find("Down").GetComponent<Image>().color = Color.yellow;
            else
                p.Find("Down").GetComponent<Image>().color = Color.gray;
        }

        for (int i = 0; i < cartNum; ++i)
        {
            Transform c = cartsUI[i];

            Text txt = c.Find("Status/Text").GetComponent<Text>();
            Image img = c.Find("Status").GetComponent<Image>();
            GameObject up = c.Find("Status/Up").gameObject;
            GameObject down = c.Find("Status/Down").gameObject;

            txt.text = sta.carts[i].pos.ToString();

            switch (sta.carts[i].state)
            {
                case CartState.idle:
                    {
                        img.color = Color.red;
                        up.SetActive(false);
                        down.SetActive(false);
                        break;
                    }
                case CartState.idle_open:
                    {
                        img.color = Color.green;
                        up.SetActive(false);
                        down.SetActive(false);
                        break;
                    }
                case CartState.up:
                    {
                        img.color = Color.red;
                        up.SetActive(true);
                        down.SetActive(false);
                        break;
                    }
                case CartState.up_open:
                    {
                        img.color = Color.green;
                        up.SetActive(true);
                        down.SetActive(false);
                        break;
                    }
                case CartState.down:
                    {
                        img.color = Color.red;
                        up.SetActive(false);
                        down.SetActive(true);
                        break;
                    }
                case CartState.down_open:
                    {
                        img.color = Color.green;
                        up.SetActive(false);
                        down.SetActive(true);
                        break;
                    }
            }

            for (int j = 0; j < levelNum; ++j)
            {
                if (sta.carts[i].stopLevels.Contains(j + 1))
                    buttons[i][j].GetComponent<Image>().color = Color.green;
                else
                    buttons[i][j].GetComponent<Image>().color = Color.white;
            }
        }
    }
}
