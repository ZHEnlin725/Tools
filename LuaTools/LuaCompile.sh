#! /bin/bash
function read_dir(){
    for file in `ls $1`
    do
        if [ -d $1"/"$file ]  #注意此处之间一定要加上空格，否则会报错
        then
            read_dir $1"/"$file
        else
            strA=$1"/"$file
            strB=".meta"
            result=$(echo $strA | grep "${strB}")
            if [[ "$result" == "" ]]
            then
                echo $1"/"$file
                luac -o $1"/"$file $1"/"$file
            fi
        fi
    done
}
#测试目录 test
workdir=$(cd $(dirname $0); pwd)
read_dir $workdir"/../Assets/tempLuaScpt"

exit 0