#!/usr/bin/env sh
sed -i "s/\${revision}/$1/g" ./charts/$APP_NAME/Chart.yaml