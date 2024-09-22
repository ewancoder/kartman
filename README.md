# KartMan - Karting Manager

![license](https://img.shields.io/github/license/ewancoder/kartman?color=blue)
![activity](https://img.shields.io/github/commit-activity/m/ewancoder/kartman)

## Production status

![ci](https://github.com/ewancoder/kartman/actions/workflows/deploy.yml/badge.svg?branch=main)
![status](https://img.shields.io/github/last-commit/ewancoder/kartman/main)
![api-coverage](https://img.shields.io/endpoint?url=https://gist.githubusercontent.com/ewancoder/0184962696ef0364be7a3f491133f2f9/raw/kartman-api-coverage-main.json)
![web-ui-coverage](https://img.shields.io/endpoint?url=https://gist.githubusercontent.com/ewancoder/0184962696ef0364be7a3f491133f2f9/raw/kartman-web-ui-coverage-main.json)

## Development status

![ci](https://github.com/ewancoder/kartman/actions/workflows/deploy.yml/badge.svg?branch=develop)
![status](https://img.shields.io/github/last-commit/ewancoder/kartman/develop)
![diff](https://img.shields.io/github/commits-difference/ewancoder/kartman?base=main&head=develop&logo=git&label=diff&color=orange)
![api-coverage](https://img.shields.io/endpoint?url=https://gist.githubusercontent.com/ewancoder/0184962696ef0364be7a3f491133f2f9/raw/kartman-api-coverage-develop.json)
![web-ui-coverage](https://img.shields.io/endpoint?url=https://gist.githubusercontent.com/ewancoder/0184962696ef0364be7a3f491133f2f9/raw/kartman-web-ui-coverage-develop.json)

## What it's about

This is an application that gathers statistics from a local Karting Track, including your time & weather conditions. It uses kart-timer API to get the telemetry data, and weatherapi to get the weather.

The following technologies were used:

- Postgresql
- Serilog for logging (to Seq)
- Angular (zoneless)
