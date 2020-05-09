using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Json
{
    private int jsonType = -1;
    private object obj = null;

    public Json(int type)
    {
        jsonType = type;

        switch (jsonType)
        {
            case 3:
                obj = new List<Json>();
                break;
            case 4:
                obj = new Dictionary<string, Json>();
                break;
            default:
                jsonType = -1;
                break;
        }
    }

    public Json(int type, int value)
    {
        jsonType = 0;
        obj = value;
    }

    public Json(int type, string value)
    {
        jsonType = 1;
        obj = value;
    }

    public Json(bool value)
    {
        jsonType = 2;
        obj = value;
    }

    public Json(int type, object that)
    {
        jsonType = type;
        obj = that;
    }

    public Json(string input)
    {
        int index = 0;

        jsonType = readType(input, ref index);

        switch (jsonType)
        {
            case 0:
                obj = IntParser(input, ref index);
                break;
            case 1:
                obj = StringParser(input, ref index);
                break;
            case 2:
                obj = BoolParser(input, ref index);
                break;
            case 3:
                obj = ListParser(input, ref index);
                break;
            case 4:
                obj = ObjectParser(input, ref index);
                break;
            default:
                Debug.LogError("Invalid JSON Input String!");
                break;
        }
    }

    public Json(Json that)
    {
        jsonType = that.jsonType;

        switch (jsonType)
        {
            case 0:
                obj = that.GetInt();
                break;
            case 1:
                obj = that.GetString();
                break;
            case 2:
                obj = that.GetBool();
                break;
            case 3:
                obj = new List<Json>();
                foreach (Json js in that.GetList())
                {
                    ((List<Json>)obj).Add(new Json(js));
                }
                break;
            case 4:
                obj = new Dictionary<string, Json>();
                foreach (KeyValuePair<string, Json> kvp in that.GetObject())
                {
                    ((Dictionary<string, Json>)obj).Add(kvp.Key, new Json(kvp.Value));
                }
                break;
            default:
                jsonType = -1;
                break;
        }
    }

    private int readType(string input, ref int index)
    {
        while (input[index] == ' ' ||
              input[index] == '\r' ||
              input[index] == '\n' ||
              input[index] == '\t')
            ++index;

        switch (input[index])
        {
            case '"':
                return 1;
            case 't':
                return 2;
            case 'f':
                return 2;
            case '[':
                return 3;
            case '{':
                return 4;
            case '-':
                return 0;
            default:
                return input[index] >= '0' && input[index] <= '9' ? 0 : -1;
        }
    }

    public string Serialize()
    {
        string ret = "";

        switch (jsonType)
        {
            case -1:
                return "Undefined JSON Data";
            case 0:
                ret += GetInt().ToString();
                break;
            case 1:
                ret += '"' + GetString() + '"';
                break;
            case 2:
                ret += GetBool().ToString();
                break;
            case 3:
                ret += '[';
                foreach(Json js in GetList())
                {
                    ret += js.Serialize();
                    ret += ',';
                }
                ret = ret.Substring(0, ret.Length - 1);
                ret += ']';
                break;
            case 4:
                ret += '{';
                foreach(KeyValuePair<string, Json> kvp in GetObject())
                {
                    ret += '"' + kvp.Key + '"';
                    ret += ':';
                    ret += kvp.Value.Serialize();
                    ret += ',';
                }
                ret = ret.Substring(0, ret.Length - 1);
                ret += '}';
                break;
        }

        return ret;
    }

    public int GetInt()
    {
        if(jsonType != 0)
        {
            Debug.LogError("NOT an int json object, this will return 0!");
            return 0;
        }

        return (int)obj;
    }

    public string GetString()
    {
        if(jsonType != 1)
        {
            Debug.LogError("NOT a string json object, this will return empty string!");
            return "";
        }

        return (string)obj;
    }

    public bool GetBool()
    {
        if(jsonType != 2)
        {
            Debug.LogError("NOT a bool json object, this will return false!");
            return false;
        }

        return (bool)obj;
    }

    public List<Json> GetList()
    {
        if (jsonType != 3)
        {
            Debug.LogError("NOT a vector json object, this will return null!");
            return null;
        }

        return (List<Json>)obj;
    }

    public Dictionary<string, Json> GetObject()
    {
        if(jsonType != 4)
        {
            Debug.LogError("NOT a object json object, this will return null!");
            return null;
        }

        return (Dictionary<string, Json>)obj;
    }

    private object NullParser(string input, ref int index)
    {
        index += 4;

        return null;
    }

    private object IntParser(string input, ref int index)
    {
        bool neg = input[index] == '-';

        if (neg)
            ++index;

        int ret = 0;

        while(input[index] <= '9' && input[index] >= '0')
        {
            ret *= 10;
            ret += input[index] - '0';
            ++index;
        }

        if (neg)
            ret = -ret;

        return ret;
    }

    private object StringParser(string input, ref int index)
    {
        string ret = "";

        ++index;

        while(input[index] != '"')
        {
            // Escape Characters
            if(input[index] == '\\')
            {
                ret += input[index];
                ++index;
            }

            ret += input[index];
            ++index;
        }

        ++index;

        return ret;
    }

    private object BoolParser(string input, ref int index)
    {
        bool ret = input[index] == 't';

        index += ret ? 4 : 5;

        return ret;
    }

    private object ListParser(string input, ref int index)
    {
        List<Json> list = new List<Json>();

        ++index;

        while(input[index] != ']')
        {
            switch (input[index])
            {
                case ' ':
                    ++index;
                    break;
                case '\r':
                    ++index;
                    break;
                case '\n':
                    ++index;
                    break;
                case '\t':
                    ++index;
                    break;
                case ',':
                    ++index;
                    break;
                case '[':
                    list.Add(new Json(3, ListParser(input, ref index)));
                    break;
                case '{':
                    list.Add(new Json(4, ObjectParser(input, ref index)));
                    break;
                case 't':
                    list.Add(new Json(2, BoolParser(input, ref index)));
                    break;
                case 'f':
                    list.Add(new Json(2, BoolParser(input, ref index)));
                    break;
                case '"':
                    list.Add(new Json(1, StringParser(input, ref index)));
                    break;
                case 'n':
                    list.Add(new Json(-1, NullParser(input, ref index)));
                    break;
                case '-':
                    list.Add(new Json(0, IntParser(input, ref index)));
                    break;
                default:
                    list.Add(new Json(0, IntParser(input, ref index)));
                    break;
            }
        }

        ++index;

        return list;
    }

    private object ObjectParser(string input, ref int index)
    {
        Dictionary<string, Json> dic = new Dictionary<string, Json>();

        ++index;

        int flag = 0;
        string column = "";
        while(input[index] != '}')
        {
            if(flag == 1)
            {
                switch (input[index])
                {
                    case ' ':
                        ++index;
                        break;
                    case '\r':
                        ++index;
                        break;
                    case '\n':
                        ++index;
                        break;
                    case '\t':
                        ++index;
                        break;
                    case ',':
                        ++index;
                        break;
                    case ':':
                        ++index;
                        break;
                    case '[':
                        dic.Add(column, new Json(3, ListParser(input, ref index)));
                        flag = 0; column = "";
                        break;
                    case '{':
                        dic.Add(column, new Json(4, ObjectParser(input, ref index)));
                        flag = 0; column = "";
                        break;
                    case 't':
                        dic.Add(column, new Json(2, BoolParser(input, ref index)));
                        flag = 0; column = "";
                        break;
                    case 'f':
                        dic.Add(column, new Json(2, BoolParser(input, ref index)));
                        flag = 0; column = "";
                        break;
                    case '"':
                        dic.Add(column, new Json(1, StringParser(input, ref index)));
                        flag = 0; column = "";
                        break;
                    case 'n':
                        dic.Add(column, new Json(-1, NullParser(input, ref index)));
                        flag = 0; column = "";
                        break;
                    case '-':
                        dic.Add(column, new Json(0, IntParser(input, ref index)));
                        flag = 0; column = "";
                        break;
                    default:
                        dic.Add(column, new Json(0, IntParser(input, ref index)));
                        flag = 0; column = "";
                        break;
                }
            }
            else
            {
                if(input[index] == '"')
                {
                    column = StringReader(input, ref index);
                    flag = 1;
                }
                else
                {
                    ++index;
                }
            }
        }

        ++index;

        return dic;
    }

    private string StringReader(string input, ref int index)
    {
        string ret = "";

        ++index;

        while (input[index] != '"')
        {
            // Escape Characters
            if (input[index] == '\\')
            {
                ret += input[index];
                ++index;
            }

            ret += input[index];
            ++index;
        }

        ++index;

        return ret;
    }
}