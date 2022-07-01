import io
import os
import sys
import json
import hashlib
import subprocess
from enum import Enum

cwd = os.getcwd()

res_folder = "resources"
diff_folder = "differences"
version_folder = "versionConf"
res_list_name = "resfilelist.json"

res_version_key = "resVersion"
min_res_version_key = "minResVersion"

version_conf_filename = "version"

unity_path = "D:\\UnityEditors\\Unity\\Editor\\Unity.exe"

minResVersionIOS = 0  # 当前ios允许使用的最小包体中的资源版本

minResVersionAndroid = 0  # 当前android允许使用的最小包体中的资源版本

auto_delete_useless_diff = False


class Platform(Enum):
    Android = 1,
    iOS = 2,


class Resource:
    class JSONEncoder(json.JSONEncoder):
        def default(self, o):
            if isinstance(o, Resource):
                return o.__dict__
            return json.JSONEncoder.default(self, o)

    def __init__(self, name, path, size, md5):
        self.name = name
        self.path = path.replace("\\", "/")
        self.size = size
        self.md5 = md5

    def __str__(self):
        return "path:" + self.path + ",size:" + self.size + ",md5:" + self.md5


def get_md5(path):
    m = hashlib.md5()
    with io.open(path, 'rb') as f:
        for line in f:
            m.update(line)
    return m.hexdigest()


def writefile(filepath, content, mode='w', encoding="utf-8"):
    with io.open(filepath, mode, encoding=encoding) as stream:
        stream.write(content)
        stream.close()


def call_unity(unity_exe_path, log_file_path, project_path, class_name, method_name):
    cmd = "%s -nographics -quit -batchmode -logFile %s -projectPath %s -executeMethod %s.%s" % (
        unity_exe_path, log_file_path, project_path, class_name, method_name)
    p = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    p.wait()
    print(cmd + "\nFinished !!!")


def gen_res_list(patch_path):
    res_list = []
    for root, dirs, files in os.walk(patch_path):
        for file in files:
            filename = file
            filepath = os.path.join(root, file)
            filesize = os.path.getsize(filepath)
            if filename.endswith(".json") or filename.endswith(".exe") or filename.endswith(
                    ".DS_store") or filename.endswith(".DS_Store"):
                continue
            res_list.append(Resource(filename, filepath, filesize, get_md5(filepath)))
    filename = patch_path + "/" + res_list_name
    writefile(filename, json.dumps({"list": res_list}, cls=Resource.JSONEncoder, ensure_ascii=False))


def get_latest_version(hotfixpath):
    latest = "0"
    if os.path.exists(hotfixpath):
        patches = os.listdir(hotfixpath)
        for filename in patches:
            if filename.isdigit():
                if int(filename) > int(latest):
                    latest = filename
    else:
        print("not exists " + hotfixpath + " !!!")

    return latest


def modify_version_file(version_filepath, field, value):
    with io.open(version_filepath, 'rb') as content:
        json_obj = json.load(content)
        json_obj[field] = value
        writefile(version_filepath, json.dumps(json_obj))


def gen_diff_file(res_filepath, diff_filepath, platform_name, min_version=1):
    if not os.path.exists(diff_filepath):
        os.mkdir(diff_filepath)
    latest_version = get_latest_version(res_filepath)
    for dirname in os.listdir(res_filepath):
        if dirname.isdigit() and not os.path.exists(os.path.join(res_filepath, dirname, res_list_name)):
            gen_res_list(os.path.join(res_filepath, dirname))
    for i in range(min_version, int(latest_version)):
        last_diff_filepath = os.path.join(diff_filepath,
                                          "diff_%s_%s_%s.json" % (platform_name, str(i), str(int(latest_version) - 1)))
        res_dict = {}
        diff_list = []
        if os.path.exists(last_diff_filepath):
            # 如果之前的差异文件列表存在则直接加上新的版本的新增文件
            with io.open(last_diff_filepath, 'rb') as content:
                res_list = json.load(content)["list"]
                for res in res_list:
                    res_dict[res["name"]] = res
                with io.open(os.path.join(res_filepath, latest_version, res_list_name), 'rb') as content:
                    res_list = json.load(content)["list"]
                    for res in res_list:
                        res_dict[res["name"]] = res
            if auto_delete_useless_diff:
                os.remove(last_diff_filepath)
        else:
            # 如果之前的差异文件列表不存在则对逐个版本的文件比对一直到最新的版本
            for j in range(i + 1, int(latest_version) + 1):
                with io.open(os.path.join(res_filepath, str(j), res_list_name), 'rb') as content:
                    res_list = json.load(content)["list"]
                    for res in res_list:
                        res_dict[res["name"]] = res
        for key in res_dict:
            diff_list.append(res_dict[key])
        path = os.path.join(diff_filepath, "diff_%s_%s_%s.json" % (platform_name, str(i), str(latest_version)))
        writefile(path, json.dumps({"list": diff_list}, cls=Resource.JSONEncoder, ensure_ascii=False))

    print(res_filepath + "\t差异文件生成完毕！！！")


def patch(platform, modify_min_version):
    platform_name = str(platform.name).lower()
    res_filepath = os.path.join(res_folder, platform_name)
    if not os.path.exists(res_filepath):
        print("该路径%s不存在,无法生成%s平台的热更补丁！！！" % (res_filepath, platform_name))
        return
    latest_version = get_latest_version(res_filepath)
    tempfilepath = os.path.join(res_filepath, "temp")
    if os.path.exists(tempfilepath):
        latest_version = int(latest_version) + 1
        path_filepath = os.path.join(res_filepath, str(latest_version))
        os.rename(tempfilepath, path_filepath)
        gen_res_list(path_filepath)
    min_version = minResVersionIOS if platform == Platform.iOS else (
        minResVersionAndroid if platform == Platform.Android else 0)
    gen_diff_file(res_filepath, diff_folder, platform_name, min_version)
    version_file = "%s/%s.%s" % (version_folder, version_conf_filename, platform_name)
    if os.path.exists:
        modify_version_file(version_file, res_version_key, latest_version)
        if modify_min_version:
            modify_version_file(version_file, min_res_version_key, latest_version)
    else:
        print(version_file + " 不存在无法修改版本控制文件信息！！！")
    print("热更文件已生成 平台:" + platform_name + ",最新版本:" + str(latest_version))


modify_min_version = len(sys.argv) > 1

for platform in list(Platform):
    patch(platform, modify_min_version)
